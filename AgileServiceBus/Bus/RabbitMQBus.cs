using AgileSB.Attributes;
using AgileSB.DTO;
using AgileSB.Exceptions;
using AgileSB.Extensions;
using AgileSB.Interfaces;
using AgileSB.Log;
using AgileSB.Utilities;
using AgileServiceBus.Interfaces;
using AgileServiceBus.Utilities;
using Autofac;
using NCrontab;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AgileSB.Bus
{
    public class RabbitMQBus : IBus
    {
        private const ushort REQUEST_PREFETCHCOUNT = 30;
        private const ushort RESPONSE_PREFETCHCOUNT = 50;
        private const int REQUEST_TIMEOUT = 7000;
        private const int DEAD_LETTER_QUEUE_RECOVERY_LIMIT = 1000;
        private const ushort MIN_RETRY_DELAY = 1;
        private const ushort MAX_RETRY_DELAY = 250;
        private const ushort RETRY_LIMIT = 5;
        private const ushort NUMBER_OF_THREADS = 5;
        private const string DEAD_LETTER_QUEUE_EXCHANGE = "dead_letter_queue";

        private IConnection _connection;
        private IModel _senderChannel;
        private IModel _requestListenerChannel;
        private IModel _responseListenerChannel;
        private Dictionary<int, IModel> _eventListenerChannels;
        private IModel _deadLetterQueueChannel;
        private string _appId;
        private string _responseRoutingKey;
        private ResponseWaiter _responseWaiter;
        private IContainer _container;
        private List<Tuple<IModel, string, EventingBasicConsumer>> _toActivateConsumers;
        private TaskDispatcher _dispatcher;

        public ILogger Logger { get; set; }
        public ContainerBuilder Container { get; }

        public RabbitMQBus(string connectionString)
        {
            Dictionary<string, string> settings = connectionString.ParseAsConnectionString();

            //creates the connection
            ConnectionFactory connectionFactory = new ConnectionFactory();
            connectionFactory.HostName = settings["HostName"];
            connectionFactory.Port = Int32.Parse(settings["Port"]);
            connectionFactory.UserName = settings["UserName"];
            connectionFactory.Password = settings["Password"];
            connectionFactory.AutomaticRecoveryEnabled = true;
            _connection = connectionFactory.CreateConnection();

            //creates channels
            _senderChannel = _connection.CreateModel();
            _requestListenerChannel = _connection.CreateModel();
            _requestListenerChannel.BasicQos(0, REQUEST_PREFETCHCOUNT, false);
            _responseListenerChannel = _connection.CreateModel();
            _responseListenerChannel.BasicQos(0, RESPONSE_PREFETCHCOUNT, false);
            _eventListenerChannels = new Dictionary<int, IModel>();
            _deadLetterQueueChannel = _connection.CreateModel();

            //application identifier
            _appId = settings["AppId"];

            //builder for container
            Container = new ContainerBuilder();

            //response waiter
            _responseWaiter = new ResponseWaiter(REQUEST_TIMEOUT);

            //response listener
            Guid responseGuid = Guid.NewGuid();
            string responseQueue = (_appId.ToLower() + "-response-" + responseGuid.ToString("N"));
            string responseExchange = (_appId.ToLower() + "_response");
            _responseRoutingKey = responseGuid.ToString("N");
            _responseListenerChannel.QueueDeclare(responseQueue, false, true, true, null);
            _responseListenerChannel.ExchangeDeclare(responseExchange, ExchangeType.Direct, true, false);
            _responseListenerChannel.QueueBind(responseQueue, responseExchange, _responseRoutingKey);
            EventingBasicConsumer consumer = new EventingBasicConsumer(_responseListenerChannel);
            consumer.Received += (obj, args) =>
            {
                _responseWaiter.Resolve(args.BasicProperties.CorrelationId, Encoding.UTF8.GetString(args.Body));
                _responseListenerChannel.BasicAck(args.DeliveryTag, false);
            };

            _responseListenerChannel.BasicConsume(responseQueue, false, consumer);

            //builded container
            _container = null;

            //list of to activate consumers
            _toActivateConsumers = new List<Tuple<IModel, string, EventingBasicConsumer>>();

            //task schedulers
            _dispatcher = new TaskDispatcher(NUMBER_OF_THREADS);
        }

        public Task<TResponse> RequestAsync<TResponse>(object request)
        {
            return (Task.Factory.StartNew(() =>
            {
                //validation
                request.Validate();

                //message direction
                string directory = request.GetType().GetCustomAttribute<QueueConfig>().Directory;
                string subdirectory = request.GetType().GetCustomAttribute<QueueConfig>().Subdirectory;
                string exchange = ("request_" + directory.ToLower() + "_" + subdirectory.ToLower());
                string routingKey = request.GetType().Name.ToLower();

                //creates exchange
                _senderChannel.ExchangeDeclare(exchange, ExchangeType.Direct, true, false);

                //correlation
                string correlationId = Guid.NewGuid().ToString();

                //waiter for response
                _responseWaiter.Register(correlationId);

                //request message
                IBasicProperties properties = _senderChannel.CreateBasicProperties();
                properties.AppId = _appId;
                properties.CorrelationId = correlationId;
                properties.Headers = new Dictionary<string, object>();
                properties.Headers.Add("ReplyToExchange", (_appId.ToLower() + "_response"));
                properties.Headers.Add("ReplyToRoutingKey", _responseRoutingKey);
                properties.Headers.Add("SendDate", DateTimeOffset.Now.Serialize());
                properties.Persistent = false;
                _senderChannel.BasicPublish(exchange, routingKey, properties, Encoding.UTF8.GetBytes(request.Serialize()));

                //waiting response
                Response<TResponse> response = null;
                string message = _responseWaiter.Wait(correlationId);
                if (message != null)
                    response = message.Deserialize<Response<TResponse>>();

                _responseWaiter.Unregister(correlationId);

                //timeout
                if (response == null)
                    throw (new TimeoutException(request.GetType().Name + " did Not Respond"));

                //remote error
                if (response.ExceptionCode != null)
                    throw (new RemoteException(response.ExceptionCode, response.ExceptionMessage));

                //success
                return (response.Data);
            }));
        }

        public async Task PublishAsync<TMessage>(TMessage message) where TMessage : class
        {
            await PublishAsync(message, null);
        }

        public Task PublishAsync<TMessage>(TMessage message, string topic) where TMessage : class
        {
            return (Task.Factory.StartNew(() =>
            {
                //validation
                message.Validate();

                //message direction
                string directory = typeof(TMessage).GetTypeInfo().GetCustomAttribute<QueueConfig>().Directory;
                string subdirectory = typeof(TMessage).GetTypeInfo().GetCustomAttribute<QueueConfig>().Subdirectory;
                string exchange = ("event_" + directory.ToLower() + "_" + subdirectory.ToLower());
                string routingKey = (typeof(TMessage).Name.ToLower() + "." + (topic != null ? topic.ToLower() : ""));

                //creates exchange
                _senderChannel.ExchangeDeclare(exchange, ExchangeType.Topic, true, false);

                //message publishing
                IBasicProperties properties = _senderChannel.CreateBasicProperties();
                properties.AppId = _appId;
                properties.MessageId = Guid.NewGuid().ToString();
                properties.Headers = new Dictionary<string, object>();
                properties.Headers.Add("SendDate", DateTimeOffset.Now.Serialize());
                properties.Headers.Add("RetryIndex", 0.Serialize());
                properties.Persistent = true;
                _senderChannel.BasicPublish(exchange, routingKey, properties, Encoding.UTF8.GetBytes(message.Serialize()));
            }));
        }

        public IIncludeForRetry Subscribe<TSubscriber, TRequest>() where TSubscriber : IRequestSubscriber<TRequest> where TRequest : class
        {
            //subscriber registration in a container
            Container.RegisterType<TSubscriber>().InstancePerLifetimeScope();

            //retry handler
            IRetry retryHandler = new RetryHandler(MIN_RETRY_DELAY, MAX_RETRY_DELAY, RETRY_LIMIT, true);

            //creates queue and exchange
            string directory = typeof(TRequest).GetTypeInfo().GetCustomAttribute<QueueConfig>().Directory;
            string subdirectory = typeof(TRequest).GetTypeInfo().GetCustomAttribute<QueueConfig>().Subdirectory;
            string exchange = ("request_" + directory.ToLower() + "_" + subdirectory.ToLower());
            string routingKey = typeof(TRequest).Name.ToLower();
            string queue = (_appId.ToLower() + "-request-" + directory.ToLower() + "-" + subdirectory.ToLower() + "-" + typeof(TRequest).Name.ToLower());
            _requestListenerChannel.ExchangeDeclare(exchange, ExchangeType.Direct, true, false);
            _requestListenerChannel.QueueDeclare(queue, true, false, false, null);
            _requestListenerChannel.QueueBind(queue, exchange, routingKey);

            //request listener
            EventingBasicConsumer consumer = new EventingBasicConsumer(_requestListenerChannel);
            consumer.Received += (obj, args) =>
            {
                _dispatcher.Dispatch(async () =>
                {
                    await LogOnRequest(queue, args);

                    //request message
                    string message = Encoding.UTF8.GetString(args.Body);

                    //response action
                    Response<object> response = new Response<object>();

                    await retryHandler.ExecuteAsync(async () =>
                    {
                        TRequest request = message.Deserialize<TRequest>();
                        request.Validate();
                        using (ILifetimeScope container = _container.BeginLifetimeScope())
                        {
                            TSubscriber subscriber = container.Resolve<TSubscriber>();
                            subscriber.Bus = this;
                            response.Data = await subscriber.ResponseAsync(request);
                        }
                    },
                    async (exception, retryIndex, retryLimit) =>
                    {
                        await LogOnResponseError(queue, args, exception, retryIndex, retryLimit);
                        response.ExceptionCode = exception.GetType().Name.Replace("Exception", "");
                        response.ExceptionMessage = exception.Message;
                    });

                    //response message
                    if (typeof(TSubscriber).GetMethod("ResponseAsync").GetCustomAttribute<FakeResponse>() == null)
                    {
                        message = response.Serialize();
                        IBasicProperties properties = _senderChannel.CreateBasicProperties();
                        properties.Persistent = false;
                        properties.CorrelationId = args.BasicProperties.CorrelationId;
                        _senderChannel.BasicPublish(Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["ReplyToExchange"]), Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["ReplyToRoutingKey"]), properties, Encoding.UTF8.GetBytes(message));

                        await LogOnResponse(queue, message, args);
                    }

                    //acknowledgment
                    _requestListenerChannel.BasicAck(args.DeliveryTag, false);
                });
            };

            _toActivateConsumers.Add(new Tuple<IModel, string, EventingBasicConsumer>(_requestListenerChannel, queue, consumer));

            return retryHandler;
        }

        public IExcludeForRetry Subscribe<TSubscriber, TMessage>(string topic, ushort prefetchCount, string retryCron, ushort? retryLimit) where TSubscriber : IPublishSubscriber<TMessage> where TMessage : class
        {
            //subscriber registration in a container
            Container.RegisterType<TSubscriber>().InstancePerLifetimeScope();

            //channel
            IModel channel;
            if (_eventListenerChannels.ContainsKey(prefetchCount) == false)
            {
                channel = _connection.CreateModel();
                channel.BasicQos(0, prefetchCount, false);
                _eventListenerChannels.Add(prefetchCount, channel);
            }
            else
                channel = _eventListenerChannels[prefetchCount];

            //retry handler
            IRetry retryHandler = new RetryHandler(0, 0, 0, false);

            //creates queue and exchanges
            string directory = typeof(TMessage).GetTypeInfo().GetCustomAttribute<QueueConfig>().Directory;
            string subdirectory = typeof(TMessage).GetTypeInfo().GetCustomAttribute<QueueConfig>().Subdirectory;
            string exchange = ("event_" + directory.ToLower() + "_" + subdirectory.ToLower());
            string routingKey = (typeof(TMessage).Name.ToLower() + "." + (topic != null ? topic.ToLower() : "*"));
            string restoreRoutingKey = (_appId.ToLower() + "." + directory.ToLower() + "." + subdirectory.ToLower() + "." + typeof(TMessage).Name.ToLower() + (topic != null ? ("." + topic.ToLower()) : ""));
            string queue = (_appId.ToLower() + "-event-" + directory.ToLower() + "-" + subdirectory.ToLower() + "-" + typeof(TMessage).Name.ToLower() + (topic != null ? ("-" + topic.ToLower()) : ""));
            channel.ExchangeDeclare(exchange, ExchangeType.Topic, true, false);
            _deadLetterQueueChannel.ExchangeDeclare(DEAD_LETTER_QUEUE_EXCHANGE, ExchangeType.Direct, true, false);
            channel.QueueDeclare(queue, true, false, false, null);
            channel.QueueBind(queue, exchange, routingKey);
            channel.QueueBind(queue, DEAD_LETTER_QUEUE_EXCHANGE, restoreRoutingKey);

            //creates dead letter queue
            string deadLetterQueue = (queue + "-dlq");
            _deadLetterQueueChannel.QueueDeclare(deadLetterQueue, true, false, false, null);
            string dlqRoutingKey = (_appId.ToLower() + "." + directory.ToLower() + "." + subdirectory.ToLower() + "." + typeof(TMessage).Name.ToLower() + (topic != null ? ("." + topic.ToLower()) : "") + ".dlq"); ;
            _deadLetterQueueChannel.QueueBind(deadLetterQueue, DEAD_LETTER_QUEUE_EXCHANGE, dlqRoutingKey);

            //message listener
            EventingBasicConsumer consumer = new EventingBasicConsumer(channel);
            consumer.Received += (obj, args) =>
            {
                _dispatcher.Dispatch(async () =>
                {
                    await LogOnPublish(queue, args);

                    try
                    {
                        TMessage message = Encoding.UTF8.GetString(args.Body).Deserialize<TMessage>();
                        message.Validate();
                        using (ILifetimeScope container = _container.BeginLifetimeScope())
                        {
                            TSubscriber subscriber = container.Resolve<TSubscriber>();
                            subscriber.Bus = this;
                            await subscriber.ConsumeAsync(message);
                        }

                        await LogOnConsumed(queue, args);
                    }
                    catch (Exception exception)
                    {
                        await LogOnConsumeError(queue, args, exception, retryLimit);

                        ushort retryIndex = (Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["RetryIndex"])).Deserialize<ushort>();
                        if (retryHandler.IsForRetry(exception) && !String.IsNullOrEmpty(retryCron) && retryLimit != null && retryIndex < retryLimit)
                        {
                            IBasicProperties properties = _deadLetterQueueChannel.CreateBasicProperties();
                            properties.AppId = _appId;
                            properties.MessageId = Guid.NewGuid().ToString();
                            properties.Headers = new Dictionary<string, object>();
                            properties.Headers.Add("SendDate", DateTime.UtcNow.Serialize());
                            properties.Headers.Add("RetryIndex", (++retryIndex).Serialize());
                            properties.Persistent = true;
                            _deadLetterQueueChannel.BasicPublish(DEAD_LETTER_QUEUE_EXCHANGE, dlqRoutingKey, properties, args.Body);
                        }
                    }

                    //acknowledgment
                    channel.BasicAck(args.DeliveryTag, false);
                });
            };

            _toActivateConsumers.Add(new Tuple<IModel, string, EventingBasicConsumer>(channel, queue, consumer));

            //message restore
            _dispatcher.Dispatch(async () =>
            {
                while (true)
                {
                    await CronDelay(retryCron);

                    List<BasicGetResult> bgrs = new List<BasicGetResult>();
                    for (int i = 0; i < DEAD_LETTER_QUEUE_RECOVERY_LIMIT; i++)
                    {
                        BasicGetResult bgr = _deadLetterQueueChannel.BasicGet(deadLetterQueue, false);
                        if (bgr == null)
                            break;

                        bgrs.Add(bgr);
                    }

                    foreach (BasicGetResult bgr in bgrs)
                    {
                        _deadLetterQueueChannel.BasicPublish(DEAD_LETTER_QUEUE_EXCHANGE, restoreRoutingKey, bgr.BasicProperties, bgr.Body);
                        _deadLetterQueueChannel.BasicAck(bgr.DeliveryTag, false);
                    }
                }
            });

            return retryHandler;
        }

        public void Schedule<TMessage>(string cron, Func<TMessage> createMessage, Func<Exception, Task> onError) where TMessage : class
        {
            _dispatcher.Dispatch(async () =>
            {
                while (true)
                {
                    try
                    {
                        await CronDelay(cron);

                        await PublishAsync(createMessage());
                    }
                    catch (Exception e)
                    {
                        await onError(e);
                    }
                }
            });
        }

        public void RegistrationCompleted()
        {
            _container = Container.Build();
            foreach (Tuple<IModel, string, EventingBasicConsumer> toActivateConsumer in _toActivateConsumers)
                toActivateConsumer.Item1.BasicConsume(toActivateConsumer.Item2, false, toActivateConsumer.Item3);
        }

        private async Task LogOnConsumed(string queueName, BasicDeliverEventArgs args)
        {
            if (Logger != null)
            {
                OnConsumed data = new OnConsumed();
                data.QueueName = queueName;
                data.Message = Encoding.UTF8.GetString(args.Body);
                data.MessageId = args.BasicProperties.MessageId;
                data.PublisherAppId = args.BasicProperties.AppId;
                data.PublishDate = (Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["SendDate"])).Deserialize<DateTime>();

                await Logger.LogAsync(data);
            }
        }

        private async Task LogOnConsumeError(string queueName, BasicDeliverEventArgs args, Exception exception, ushort? retryLimit)
        {
            if (Logger != null)
            {
                OnConsumeError data = new OnConsumeError();
                data.QueueName = queueName;
                data.Message = Encoding.UTF8.GetString(args.Body);
                data.MessageId = args.BasicProperties.MessageId;
                data.Exception = exception;
                data.RetryIndex = (Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["RetryIndex"])).Deserialize<uint>();
                data.RetryLimit = retryLimit;
                data.PublisherAppId = args.BasicProperties.AppId;
                data.PublishDate = (Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["SendDate"])).Deserialize<DateTime>();

                await Logger.LogAsync(data);
            }
        }

        private async Task LogOnPublish(string queueName, BasicDeliverEventArgs args)
        {
            if (Logger != null)
            {
                OnPublish data = new OnPublish();
                data.QueueName = queueName;
                data.Message = Encoding.UTF8.GetString(args.Body);
                data.MessageId = args.BasicProperties.MessageId;
                data.PublisherAppId = args.BasicProperties.AppId;
                data.PublishDate = (Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["SendDate"])).Deserialize<DateTime>();

                await Logger.LogAsync(data);
            }
        }

        private async Task LogOnRequest(string queueName, BasicDeliverEventArgs args)
        {
            if (Logger != null)
            {
                OnRequest data = new OnRequest();
                data.QueueName = queueName;
                data.Request = Encoding.UTF8.GetString(args.Body);
                data.CorrelationId = args.BasicProperties.CorrelationId;
                data.RequesterAppId = args.BasicProperties.AppId;
                data.RequestDate = (Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["SendDate"])).Deserialize<DateTime>();

                await Logger.LogAsync(data);
            }
        }

        private async Task LogOnResponse(string queueName, string response, BasicDeliverEventArgs args)
        {
            if (Logger != null)
            {
                OnResponse data = new OnResponse();
                data.RequestQueueName = queueName;
                data.Response = response;
                data.CorrelationId = args.BasicProperties.CorrelationId;
                data.RequesterAppId = args.BasicProperties.AppId;
                data.RequestDate = (Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["SendDate"])).Deserialize<DateTime>();

                await Logger.LogAsync(data);
            }
        }

        private async Task LogOnResponseError(string queueName, BasicDeliverEventArgs args, Exception exception, ushort retryIndex, ushort retryLimit)
        {
            if (Logger != null)
            {
                OnResponseError data = new OnResponseError();
                data.RequestQueueName = queueName;
                data.Request = Encoding.UTF8.GetString(args.Body);
                data.CorrelationId = args.BasicProperties.CorrelationId;
                data.Exception = exception;
                data.RetryIndex = retryIndex;
                data.RetryLimit = retryLimit;
                data.RequesterAppId = args.BasicProperties.AppId;
                data.RequestDate = (Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["SendDate"])).Deserialize<DateTime>();

                await Logger.LogAsync(data);
            }
        }

        private async Task CronDelay(string cron)
        {
            CrontabSchedule schedule = CrontabSchedule.Parse(cron);
            DateTime nextDate = schedule.GetNextOccurrence(DateTime.UtcNow);
            TimeSpan delay = (nextDate - DateTime.UtcNow);

            await Task.Delay(delay);
        }

        public void Dispose()
        {
            _senderChannel.Dispose();
            _requestListenerChannel.Dispose();
            _responseListenerChannel.Dispose();
            _deadLetterQueueChannel.Dispose();
            foreach (KeyValuePair<int, IModel> entry in _eventListenerChannels)
                entry.Value.Dispose();

            _connection.Dispose();
        }
    }
}