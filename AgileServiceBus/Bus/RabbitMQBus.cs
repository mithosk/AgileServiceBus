using Autofac;
using NCrontab;
using AgileSB.Attributes;
using AgileSB.DTO;
using AgileSB.Exceptions;
using AgileSB.Extensions;
using AgileSB.Interfaces;
using AgileSB.Log;
using AgileSB.Utilities;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
        private const ushort RETRY_LIMIT = 5;
        private const ushort MAX_RETRY_DELAY = 250;
        private const ushort NUMBER_OF_THREADS = 5;

        private IConnection _connection;
        private IModel _senderChannel;
        private IModel _requestSubscriberChannel;
        private IModel _responseChannel;
        private Dictionary<int, IModel> _publishSubscriberChannels;
        private IModel _deadLetterQueueChannel;
        private string _appId;
        private string _responseQueue;
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
            _requestSubscriberChannel = _connection.CreateModel();
            _requestSubscriberChannel.BasicQos(0, REQUEST_PREFETCHCOUNT, false);
            _responseChannel = _connection.CreateModel();
            _responseChannel.BasicQos(0, RESPONSE_PREFETCHCOUNT, false);
            _publishSubscriberChannels = new Dictionary<int, IModel>();
            _deadLetterQueueChannel = _connection.CreateModel();

            //application identifier
            _appId = settings["AppId"];

            //builder for container
            Container = new ContainerBuilder();

            //response waiter
            _responseWaiter = new ResponseWaiter(REQUEST_TIMEOUT);

            //response listener
            _responseQueue = (_appId + "." + Guid.NewGuid().ToString());
            _responseChannel.QueueDeclare(_responseQueue, false, true, true, null);
            EventingBasicConsumer consumer = new EventingBasicConsumer(_responseChannel);
            consumer.Received += (obj, args) =>
            {
                _responseWaiter.Resolve(args.BasicProperties.CorrelationId, Encoding.UTF8.GetString(args.Body));
                _responseChannel.BasicAck(args.DeliveryTag, false);
            };

            _responseChannel.BasicConsume(_responseQueue, false, consumer);

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

                //exchange
                string directory = request.GetType().GetCustomAttribute<QueueConfig>().Directory;
                string exchange = (directory + ".Request." + request.GetType().Name);

                //correlation
                string correlationId = Guid.NewGuid().ToString();

                //waiter for response
                _responseWaiter.Register(correlationId);

                //request message
                IBasicProperties properties = _senderChannel.CreateBasicProperties();
                properties.AppId = _appId;
                properties.ReplyTo = _responseQueue;
                properties.CorrelationId = correlationId;
                properties.Headers = new Dictionary<string, object>();
                properties.Headers.Add("SendDate", DateTimeOffset.Now.Serialize());
                properties.Persistent = false;
                _senderChannel.BasicPublish(exchange, "", properties, Encoding.UTF8.GetBytes(request.Serialize()));

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

                //creates queue and exchange
                string directory = typeof(TMessage).GetTypeInfo().GetCustomAttribute<QueueConfig>().Directory;
                string subdirectory = typeof(TMessage).GetTypeInfo().GetCustomAttribute<QueueConfig>().Subdirectory;
                string exchange = (directory + ".Message." + typeof(TMessage).Name);
                string queue = (directory + "." + subdirectory + ".Message." + typeof(TMessage).Name + (topic == null ? "" : "." + topic));
                _senderChannel.QueueDeclare(queue, true, false, false, null);
                _senderChannel.ExchangeDeclare(exchange, ExchangeType.Topic, true, false);
                _senderChannel.QueueBind(queue, exchange, (topic ?? ""));

                //message publishing
                IBasicProperties properties = _senderChannel.CreateBasicProperties();
                properties.AppId = _appId;
                properties.MessageId = Guid.NewGuid().ToString();
                properties.Headers = new Dictionary<string, object>();
                properties.Headers.Add("SendDate", DateTimeOffset.Now.Serialize());
                properties.Headers.Add("RetryIndex", 0.Serialize());
                properties.Persistent = true;
                _senderChannel.BasicPublish(exchange, (topic ?? ""), properties, Encoding.UTF8.GetBytes(message.Serialize()));
            }));
        }

        public void Subscribe<TSubscriber, TRequest>(bool retry) where TSubscriber : IRequestSubscriber<TRequest> where TRequest : class
        {
            //subscriber registration in a container
            Container.RegisterType<TSubscriber>().InstancePerLifetimeScope();

            //creates queue and exchange
            string directory = typeof(TRequest).GetTypeInfo().GetCustomAttribute<QueueConfig>().Directory;
            string subdirectory = typeof(TRequest).GetTypeInfo().GetCustomAttribute<QueueConfig>().Subdirectory;
            string exchange = (directory + ".Request." + typeof(TRequest).Name);
            string queue = (directory + "." + subdirectory + ".Request." + typeof(TRequest).Name);
            _requestSubscriberChannel.ExchangeDeclare(exchange, ExchangeType.Fanout, true, false);
            _requestSubscriberChannel.QueueDeclare(queue, true, false, false, null);
            _requestSubscriberChannel.QueueBind(queue, exchange, "");

            //request listener
            EventingBasicConsumer consumer = new EventingBasicConsumer(_requestSubscriberChannel);
            consumer.Received += (obj, args) =>
            {
                _dispatcher.Dispatch(async () =>
                {
                    await LogOnRequest(queue, args);

                    //request message
                    string message = Encoding.UTF8.GetString(args.Body);

                    //response action
                    Response<object> response = new Response<object>();
                    ushort retryLimit = (retry == true ? RETRY_LIMIT : ((ushort)0));
                    Random rnd = new Random();
                    for (ushort i = 0; i <= retryLimit; i++)
                    {
                        try
                        {
                            TRequest request = message.Deserialize<TRequest>();
                            request.Validate();
                            using (ILifetimeScope container = _container.BeginLifetimeScope())
                            {
                                TSubscriber subscriber = container.Resolve<TSubscriber>();
                                subscriber.Bus = this;
                                response.Data = await subscriber.ResponseAsync(request);
                            }

                            break;
                        }
                        catch (ValidationException e)
                        {
                            await LogOnResponseError(queue, args, e, 0, 0);
                            response.ExceptionCode = "Validation";
                            response.ExceptionMessage = e.Message;
                            break;
                        }
                        catch (Exception e)
                        {
                            await LogOnResponseError(queue, args, e, i, retryLimit);
                            response.ExceptionCode = e.GetType().Name.Replace("Exception", "");
                            response.ExceptionMessage = e.Message;
                            if (i < retryLimit)
                                await Task.Delay(rnd.Next(0, MAX_RETRY_DELAY));
                        }
                    }

                    //response message
                    if (typeof(TSubscriber).GetMethod("ResponseAsync").GetCustomAttribute<FakeResponse>() == null)
                    {
                        message = response.Serialize();
                        IBasicProperties properties = _senderChannel.CreateBasicProperties();
                        properties.Persistent = false;
                        properties.CorrelationId = args.BasicProperties.CorrelationId;
                        _senderChannel.BasicPublish("", args.BasicProperties.ReplyTo, properties, Encoding.UTF8.GetBytes(message));

                        await LogOnResponse(queue, message, args);
                    }

                    //acknowledgment
                    _requestSubscriberChannel.BasicAck(args.DeliveryTag, false);
                });
            };

            _toActivateConsumers.Add(new Tuple<IModel, string, EventingBasicConsumer>(_requestSubscriberChannel, queue, consumer));
        }

        public void Subscribe<TSubscriber, TMessage>(string topic, ushort prefetchCount, ushort? retryLimit, TimeSpan? retryDelay) where TSubscriber : IPublishSubscriber<TMessage> where TMessage : class
        {
            //subscriber registration in a container
            Container.RegisterType<TSubscriber>().InstancePerLifetimeScope();

            //creates queue and exchange
            string directory = typeof(TMessage).GetTypeInfo().GetCustomAttribute<QueueConfig>().Directory;
            string subdirectory = typeof(TMessage).GetTypeInfo().GetCustomAttribute<QueueConfig>().Subdirectory;
            string exchange = (directory + ".Message." + typeof(TMessage).Name);
            string queue = (directory + "." + subdirectory + ".Message." + typeof(TMessage).Name + (topic == null ? "" : ("." + topic)));
            _requestSubscriberChannel.ExchangeDeclare(exchange, ExchangeType.Topic, true, false);
            _requestSubscriberChannel.QueueDeclare(queue, true, false, false, null);
            _requestSubscriberChannel.QueueBind(queue, exchange, (topic ?? "#"));

            //creates dead letter queue
            string deadLetterQueue = (queue + ".DLQ");
            _deadLetterQueueChannel.QueueDeclare(deadLetterQueue, true, false, false, null);

            //channel
            IModel channel;
            if (_publishSubscriberChannels.ContainsKey(prefetchCount) == false)
            {
                channel = _connection.CreateModel();
                channel.BasicQos(0, prefetchCount, false);
                _publishSubscriberChannels.Add(prefetchCount, channel);
            }
            else
                channel = _publishSubscriberChannels[prefetchCount];

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
                    catch (ValidationException e)
                    {
                        await LogOnConsumeError(queue, args, e, 0);
                    }
                    catch (Exception e)
                    {
                        await LogOnConsumeError(queue, args, e, retryLimit);

                        uint retryIndex = (Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["RetryIndex"])).Deserialize<uint>();
                        if (retryDelay != null && (retryLimit == null || retryIndex < retryLimit))
                        {
                            IBasicProperties properties = _deadLetterQueueChannel.CreateBasicProperties();
                            properties.AppId = _appId;
                            properties.MessageId = Guid.NewGuid().ToString();
                            properties.Headers = new Dictionary<string, object>();
                            properties.Headers.Add("SendDate", DateTime.UtcNow.Serialize());
                            properties.Headers.Add("RetryIndex", (++retryIndex).Serialize());
                            properties.Persistent = true;
                            _senderChannel.BasicPublish("", deadLetterQueue, properties, args.Body);
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
                    await Task.Delay(retryDelay.Value);

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
                        _deadLetterQueueChannel.BasicPublish("", queue, bgr.BasicProperties, bgr.Body);
                        _deadLetterQueueChannel.BasicAck(bgr.DeliveryTag, false);
                    }
                }
            });
        }

        public void Schedule<TMessage>(string cron, Func<TMessage> createMessage, Func<Exception, Task> onError) where TMessage : class
        {
            _dispatcher.Dispatch(async () =>
            {
                while (true)
                {
                    try
                    {
                        CrontabSchedule schedule = CrontabSchedule.Parse(cron);
                        DateTime nextDate = schedule.GetNextOccurrence(DateTime.UtcNow);
                        TimeSpan delay = (nextDate - DateTime.UtcNow);
                        await Task.Delay(delay);

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

        public void Dispose()
        {
            _senderChannel.Dispose();
            _requestSubscriberChannel.Dispose();
            _responseChannel.Dispose();
            _deadLetterQueueChannel.Dispose();
            foreach (KeyValuePair<int, IModel> entry in _publishSubscriberChannels)
                entry.Value.Dispose();

            _connection.Dispose();
        }
    }
}