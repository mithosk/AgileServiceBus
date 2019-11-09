using AgileSB.Attributes;
using AgileSB.Exceptions;
using AgileSB.Extensions;
using AgileSB.Interfaces;
using AgileSB.Log;
using AgileServiceBus.Exceptions;
using AgileServiceBus.Extensions;
using AgileServiceBus.Interfaces;
using AgileServiceBus.Logging;
using AgileServiceBus.Tracing;
using AgileServiceBus.Utilities;
using Autofac;
using FluentValidation;
using NCrontab;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AgileSB.Bus
{
    public class RabbitMQBus : IBus
    {
        private const ushort RESPONDER_PREFETCHCOUNT = 30;
        private const ushort EVENT_HANDLER_PREFETCHCOUNT = 8;
        private const ushort REQUEST_TIMEOUT = 7000;
        private const int DEAD_LETTER_QUEUE_RECOVERY_LIMIT = 1000;
        private const ushort MIN_RETRY_DELAY = 1;
        private const ushort MAX_RETRY_DELAY = 250;
        private const ushort RETRY_LIMIT = 5;
        private const string DEAD_LETTER_QUEUE_EXCHANGE = "dead_letter_queue";
        private const string DIRECT_REPLY_QUEUE = "amq.rabbitmq.reply-to";
        private const byte SEND_NUMBER_OF_THREADS = 4;
        private const byte RECEIVE_NUMBER_OF_THREADS = 16;
        private const byte DEAD_LETTER_QUEUE_NUMBER_OF_THREADS = 1;

        private IConnection _connection;
        private IModel _requestChannel;
        private IModel _notifyChannel;
        private IModel _responderChannel;
        private IModel _eventHandlerChannel;
        private IModel _deadLetterQueueChannel;
        private string _appId;
        private MessageGroupQueue _responseQueue;
        private IContainer _container;
        private List<Tuple<IModel, string, EventingBasicConsumer>> _toActivateConsumers;
        private MultiThreadTaskScheduler _sendTaskScheduler;
        private MultiThreadTaskScheduler _receiveTaskScheduler;
        private MultiThreadTaskScheduler _deadLetterQueueTaskScheduler;
        private CancellationTokenSource _cancellationTokenSource;
        private Type _loggerType;
        private Logger _logger;
        private Type _tracerType;
        private Tracer _tracer;
        private JsonConverter _jsonConverter;

        public ILogger Logger { get; set; }
        public ContainerBuilder Container { get; }

        public RabbitMQBus(string connectionString)
        {
            Dictionary<string, string> settings = connectionString.ParseAsConnectionString();

            //creates the connection
            ConnectionFactory connectionFactory = new ConnectionFactory();
            connectionFactory.HostName = settings["HostName"];
            connectionFactory.VirtualHost = "/";
            connectionFactory.Port = Int32.Parse(settings["Port"]);
            connectionFactory.UserName = settings["UserName"];
            connectionFactory.Password = settings["Password"];
            connectionFactory.AutomaticRecoveryEnabled = true;
            _connection = connectionFactory.CreateConnection();

            //creates channels
            _requestChannel = _connection.CreateModel();
            _notifyChannel = _connection.CreateModel();
            _responderChannel = _connection.CreateModel();
            _responderChannel.BasicQos(0, RESPONDER_PREFETCHCOUNT, false);
            _eventHandlerChannel = _connection.CreateModel();
            _eventHandlerChannel.BasicQos(0, EVENT_HANDLER_PREFETCHCOUNT, false);
            _deadLetterQueueChannel = _connection.CreateModel();

            //application identifier
            _appId = settings["AppId"];

            //builder for container
            Container = new ContainerBuilder();

            //response queue
            _responseQueue = new MessageGroupQueue(REQUEST_TIMEOUT);

            //response listener         
            EventingBasicConsumer consumer = new EventingBasicConsumer(_requestChannel);
            consumer.Received += (obj, args) =>
            {
                _responseQueue.AddMessage(Encoding.UTF8.GetString(args.Body), args.BasicProperties.CorrelationId);
            };

            _requestChannel.BasicConsume(DIRECT_REPLY_QUEUE, true, consumer);

            //builded container
            _container = null;

            //list of to activate consumers
            _toActivateConsumers = new List<Tuple<IModel, string, EventingBasicConsumer>>();

            //custom task schedulers
            _sendTaskScheduler = new MultiThreadTaskScheduler(SEND_NUMBER_OF_THREADS);
            _receiveTaskScheduler = new MultiThreadTaskScheduler(RECEIVE_NUMBER_OF_THREADS);
            _deadLetterQueueTaskScheduler = new MultiThreadTaskScheduler(DEAD_LETTER_QUEUE_NUMBER_OF_THREADS);

            //cancellation token
            _cancellationTokenSource = new CancellationTokenSource();

            //default logger
            _loggerType = typeof(DefaultLogger);

            //default tracer
            _tracerType = typeof(DefaultTracer);

            //json converter
            _jsonConverter = new JsonConverter();
        }

        public void RegisterLogger<TLogger>() where TLogger : Logger
        {
            _loggerType = typeof(TLogger);
        }

        public void RegisterTracer<TTracer>() where TTracer : Tracer
        {
            _tracerType = typeof(TTracer);
        }

        public async Task<TResponse> RequestAsync<TResponse>(object request)
        {
            return await RequestAsync<TResponse>(request, null, null);
        }

        public async Task RequestAsync(object message)
        {
            await RequestAsync<object>(message);
        }

        public async Task<TResponse> RequestAsync<TResponse>(object request, ITraceScope traceScope)
        {
            string directory = request.GetType().GetCustomAttribute<QueueConfig>().Directory;
            string subdirectory = request.GetType().GetCustomAttribute<QueueConfig>().Subdirectory;

            using (ITraceScope traceSubScope = traceScope.CreateSubScope("Request-" + directory + "." + subdirectory + "." + request.GetType().Name))
                return await RequestAsync<TResponse>(request, traceSubScope.SpanId, traceSubScope.TraceId);
        }

        public async Task RequestAsync(object message, ITraceScope traceScope)
        {
            await RequestAsync<object>(message, traceScope);
        }

        public async Task NotifyAsync<TEvent>(TEvent message) where TEvent : class
        {
            await NotifyAsync(message, null);
        }

        public async Task NotifyAsync<TEvent>(TEvent message, string tag) where TEvent : class
        {
            //message direction
            string directory = typeof(TEvent).GetTypeInfo().GetCustomAttribute<QueueConfig>().Directory;
            string subdirectory = typeof(TEvent).GetTypeInfo().GetCustomAttribute<QueueConfig>().Subdirectory;
            string exchange = "event_" + directory.ToLower() + "_" + subdirectory.ToLower();
            string routingKey = typeof(TEvent).Name.ToLower() + "." + (tag != null ? tag.ToLower() : "");

            //message publishing
            await Task.Factory.StartNew(() =>
            {
                _notifyChannel.ExchangeDeclare(exchange, ExchangeType.Topic, true, false);

                IBasicProperties properties = _notifyChannel.CreateBasicProperties();
                properties.MessageId = Guid.NewGuid().ToString();
                properties.AppId = _appId;
                properties.Headers = new Dictionary<string, object>();
                properties.Headers.Add("SendDate", DateTime.UtcNow.ToString(CultureInfo.InvariantCulture));
                properties.Headers.Add("RetryIndex", "0");
                properties.Persistent = true;
                _notifyChannel.BasicPublish(exchange, routingKey, properties, Encoding.UTF8.GetBytes(_jsonConverter.Serialize(message)));
            },
            _cancellationTokenSource.Token,
            TaskCreationOptions.DenyChildAttach,
            _sendTaskScheduler);
        }

        public IIncludeForRetry Subscribe<TSubscriber, TRequest>(AbstractValidator<TRequest> validator) where TSubscriber : IResponder<TRequest> where TRequest : class
        {
            //subscriber registration in a container
            Container.RegisterType<TSubscriber>().InstancePerLifetimeScope();

            //retry handler
            IRetry retryHandler = new RetryHandler(MIN_RETRY_DELAY, MAX_RETRY_DELAY, RETRY_LIMIT, true);

            //creates queue and exchange
            string directory = typeof(TRequest).GetTypeInfo().GetCustomAttribute<QueueConfig>().Directory;
            string subdirectory = typeof(TRequest).GetTypeInfo().GetCustomAttribute<QueueConfig>().Subdirectory;
            string exchange = "request_" + directory.ToLower() + "_" + subdirectory.ToLower();
            string routingKey = typeof(TRequest).Name.ToLower();
            string queue = _appId.ToLower() + "-request-" + directory.ToLower() + "-" + subdirectory.ToLower() + "-" + typeof(TRequest).Name.ToLower();
            _responderChannel.ExchangeDeclare(exchange, ExchangeType.Direct, true, false);
            _responderChannel.QueueDeclare(queue, true, false, false, null);
            _responderChannel.QueueBind(queue, exchange, routingKey);

            //request listener
            EventingBasicConsumer consumer = new EventingBasicConsumer(_responderChannel);
            consumer.Received += (obj, args) =>
            {
                Task.Factory.StartNew(async () =>
                {
                    await LogOnRequest(queue, args);

                    //request message
                    string message = Encoding.UTF8.GetString(args.Body);

                    //tracing data
                    string traceSpanId = Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["TraceSpanId"]);
                    string traceId = Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["TraceId"]);
                    string traceDisplayName = "Respond-" + directory + "." + subdirectory + "." + typeof(TRequest).Name;

                    //response action
                    ResponseWrapper<object> responseWrapper = null;

                    await retryHandler.ExecuteAsync(async () =>
                    {
                        TRequest request = _jsonConverter.Deserialize<TRequest>(message);
                        if (validator != null)
                            await validator.ValidateAndThrowAsync(request, (directory + "." + subdirectory + "." + request.GetType().Name + " is not valid"));

                        using (ILifetimeScope container = _container.BeginLifetimeScope())
                        using (ITraceScope traceScope = ((traceSpanId != null && traceId != null) ? new TraceScope(traceSpanId, traceId, traceDisplayName, _tracer) : new TraceScope(traceDisplayName, _tracer)))
                        {
                            TSubscriber subscriber = container.Resolve<TSubscriber>();
                            subscriber.Bus = this;
                            subscriber.TraceScope = traceScope;
                            traceScope.Attributes.Add("AppId", _appId);
                            traceScope.Attributes.Add("MessageId", args.BasicProperties.MessageId);
                            responseWrapper = new ResponseWrapper<object>(await subscriber.RespondAsync(request));
                        }
                    },
                    async (exception, retryIndex, retryLimit) =>
                    {
                        await LogOnResponseError(queue, args, exception, retryIndex, retryLimit);
                        responseWrapper = new ResponseWrapper<object>(exception);
                    });

                    //response message
                    if (typeof(TSubscriber).GetMethod("RespondAsync").GetCustomAttribute<FakeResponse>() == null)
                    {
                        message = _jsonConverter.Serialize(responseWrapper);
                        IBasicProperties properties = _responderChannel.CreateBasicProperties();
                        properties.MessageId = Guid.NewGuid().ToString();
                        properties.Persistent = false;
                        properties.CorrelationId = args.BasicProperties.CorrelationId;
                        _responderChannel.BasicPublish("", args.BasicProperties.ReplyTo, properties, Encoding.UTF8.GetBytes(message));

                        await LogOnResponse(queue, message, args);
                    }

                    //acknowledgment
                    _responderChannel.BasicAck(args.DeliveryTag, false);
                },
                _cancellationTokenSource.Token,
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);
            };

            _toActivateConsumers.Add(new Tuple<IModel, string, EventingBasicConsumer>(_responderChannel, queue, consumer));

            return retryHandler;
        }

        public IExcludeForRetry Subscribe<TSubscriber, TEvent>(string tag, AbstractValidator<TEvent> validator, string retryCron, ushort? retryLimit) where TSubscriber : IEventHandler<TEvent> where TEvent : class
        {
            //naming validation
            if (tag != null)
                CheckQueueNaming(tag, "Invalid tag");

            //subscriber registration in a container
            Container.RegisterType<TSubscriber>().InstancePerLifetimeScope();

            //retry handler
            IRetry retryHandler = new RetryHandler(0, 0, 0, false);

            //creates queue and exchanges
            string directory = typeof(TEvent).GetTypeInfo().GetCustomAttribute<QueueConfig>().Directory;
            string subdirectory = typeof(TEvent).GetTypeInfo().GetCustomAttribute<QueueConfig>().Subdirectory;
            string exchange = "event_" + directory.ToLower() + "_" + subdirectory.ToLower();
            string routingKey = typeof(TEvent).Name.ToLower() + "." + (tag != null ? tag.ToLower() : "*");
            string restoreRoutingKey = _appId.ToLower() + "." + directory.ToLower() + "." + subdirectory.ToLower() + "." + typeof(TEvent).Name.ToLower() + (tag != null ? ("." + tag.ToLower()) : "");
            string queue = _appId.ToLower() + "-event-" + directory.ToLower() + "-" + subdirectory.ToLower() + "-" + typeof(TEvent).Name.ToLower() + (tag != null ? ("-" + tag.ToLower()) : "");
            _eventHandlerChannel.ExchangeDeclare(exchange, ExchangeType.Topic, true, false);
            _eventHandlerChannel.ExchangeDeclare(DEAD_LETTER_QUEUE_EXCHANGE, ExchangeType.Direct, true, false);
            _eventHandlerChannel.QueueDeclare(queue, true, false, false, null);
            _eventHandlerChannel.QueueBind(queue, exchange, routingKey);
            _eventHandlerChannel.QueueBind(queue, DEAD_LETTER_QUEUE_EXCHANGE, restoreRoutingKey);

            //creates dead letter queue
            string deadLetterQueue = (queue + "-dlq");
            string dlqRoutingKey = (_appId.ToLower() + "." + directory.ToLower() + "." + subdirectory.ToLower() + "." + typeof(TEvent).Name.ToLower() + (tag != null ? ("." + tag.ToLower()) : "") + ".dlq");
            _deadLetterQueueChannel.QueueDeclare(deadLetterQueue, true, false, false, null);
            _deadLetterQueueChannel.QueueBind(deadLetterQueue, DEAD_LETTER_QUEUE_EXCHANGE, dlqRoutingKey);

            //message listener
            EventingBasicConsumer consumer = new EventingBasicConsumer(_eventHandlerChannel);
            consumer.Received += (obj, args) =>
            {
                Task.Factory.StartNew(async () =>
                {
                    await LogOnPublish(queue, args);

                    try
                    {
                        TEvent message = _jsonConverter.Deserialize<TEvent>(Encoding.UTF8.GetString(args.Body));
                        if (validator != null)
                            await validator.ValidateAndThrowAsync(message, (directory + "." + subdirectory + "." + message.GetType().Name + " is not valid"));

                        using (ILifetimeScope container = _container.BeginLifetimeScope())
                        using (ITraceScope traceScope = new TraceScope("Handle-" + directory + "." + subdirectory + "." + typeof(TEvent).Name, _tracer))
                        {
                            TSubscriber subscriber = container.Resolve<TSubscriber>();
                            subscriber.Bus = this;
                            subscriber.TraceScope = traceScope;
                            traceScope.Attributes.Add("AppId", _appId);
                            traceScope.Attributes.Add("MessageId", args.BasicProperties.MessageId);
                            await subscriber.HandleAsync(message);
                        }

                        await LogOnConsumed(queue, args);
                    }
                    catch (Exception exception)
                    {
                        await LogOnConsumeError(queue, args, exception, retryLimit);

                        ushort retryIndex = ushort.Parse(Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["RetryIndex"]));
                        if (retryHandler.IsForRetry(exception) && !String.IsNullOrEmpty(retryCron) && retryLimit != null && retryIndex < retryLimit)
                        {
                            IBasicProperties properties = _deadLetterQueueChannel.CreateBasicProperties();
                            properties.MessageId = args.BasicProperties.MessageId;
                            properties.AppId = _appId;
                            properties.Headers = new Dictionary<string, object>();
                            properties.Headers.Add("SendDate", DateTime.UtcNow.ToString(CultureInfo.InvariantCulture));
                            properties.Headers.Add("RetryIndex", (++retryIndex).ToString());
                            properties.Persistent = true;
                            _deadLetterQueueChannel.BasicPublish(DEAD_LETTER_QUEUE_EXCHANGE, dlqRoutingKey, properties, args.Body);
                        }
                    }

                    //acknowledgment
                    _eventHandlerChannel.BasicAck(args.DeliveryTag, false);
                },
                _cancellationTokenSource.Token,
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);
            };

            _toActivateConsumers.Add(new Tuple<IModel, string, EventingBasicConsumer>(_eventHandlerChannel, queue, consumer));

            //message restore
            Task.Factory.StartNew(async () =>
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
            },
            _cancellationTokenSource.Token,
            TaskCreationOptions.DenyChildAttach,
            _deadLetterQueueTaskScheduler);

            return retryHandler;
        }

        public void Schedule<TMessage>(string cron, Func<TMessage> createMessage, Func<Exception, Task> onError) where TMessage : class
        {
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    try
                    {
                        await CronDelay(cron);

                        await NotifyAsync(createMessage());
                    }
                    catch (Exception e)
                    {
                        await onError(e);
                    }
                }
            },
            _cancellationTokenSource.Token,
            TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);
        }

        public void RegistrationCompleted()
        {
            _container = Container.Build();

            _logger = (Logger)Activator.CreateInstance(_loggerType);
            _tracer = (Tracer)Activator.CreateInstance(_tracerType);

            foreach (Tuple<IModel, string, EventingBasicConsumer> toActivateConsumer in _toActivateConsumers)
                toActivateConsumer.Item1.BasicConsume(toActivateConsumer.Item2, false, toActivateConsumer.Item3);
        }

        private async Task<TResponse> RequestAsync<TResponse>(object request, string traceSpanId, string traceId)
        {
            //message direction
            string directory = request.GetType().GetCustomAttribute<QueueConfig>().Directory;
            string subdirectory = request.GetType().GetCustomAttribute<QueueConfig>().Subdirectory;
            string exchange = "request_" + directory.ToLower() + "_" + subdirectory.ToLower();
            string routingKey = request.GetType().Name.ToLower();

            //correlation
            string correlationId = Guid.NewGuid().ToString();

            //sending request
            await Task.Factory.StartNew(() =>
            {
                _requestChannel.ExchangeDeclare(exchange, ExchangeType.Direct, true, false);

                _responseQueue.AddGroup(correlationId);

                IBasicProperties properties = _requestChannel.CreateBasicProperties();
                properties.MessageId = Guid.NewGuid().ToString();
                properties.AppId = _appId;
                properties.ReplyTo = DIRECT_REPLY_QUEUE;
                properties.CorrelationId = correlationId;
                properties.Headers = new Dictionary<string, object>();
                properties.Headers.Add("SendDate", DateTime.Now.ToString(CultureInfo.InvariantCulture));
                properties.Headers.Add("TraceSpanId", traceSpanId);
                properties.Headers.Add("TraceId", traceId);
                properties.Persistent = false;
                _requestChannel.BasicPublish(exchange, routingKey, properties, Encoding.UTF8.GetBytes(_jsonConverter.Serialize(request)));
            },
            _cancellationTokenSource.Token,
            TaskCreationOptions.DenyChildAttach,
            _sendTaskScheduler);

            //response object
            ResponseWrapper<TResponse> responseWrapper = null;

            //waiting response
            await Task.Factory.StartNew(() =>
            {
                string message = _responseQueue.WaitMessage(correlationId);
                if (message != null)
                    responseWrapper = _jsonConverter.Deserialize<ResponseWrapper<TResponse>>(message);

                _responseQueue.RemoveGroup(correlationId);
            },
            _cancellationTokenSource.Token,
            TaskCreationOptions.DenyChildAttach,
            _receiveTaskScheduler);

            //timeout
            if (responseWrapper == null)
                throw (new TimeoutException(request.GetType().Name + " did Not Respond"));

            //remote error
            if (responseWrapper.ExceptionCode != null)
                throw new RemoteException(responseWrapper.ExceptionCode, responseWrapper.ExceptionMessage);

            //response
            return responseWrapper.Response;
        }

        private void CheckQueueNaming(string word, string exceptionMessage)
        {
            //validation with regular expression
            Regex regex = new Regex("^[a-zA-Z0-9]+$");
            if (!regex.IsMatch(word ?? ""))
                throw new QueueNamingException(exceptionMessage);

            //forbidden words
            switch (word.ToLower())
            {
                case "request":
                case "response":
                case "event":
                case "dlq":
                    throw new QueueNamingException(exceptionMessage);
            }
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
                data.PublishDate = DateTime.Parse(Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["SendDate"]), CultureInfo.InvariantCulture);

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
                data.RetryIndex = uint.Parse(Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["RetryIndex"]));
                data.RetryLimit = retryLimit;
                data.PublisherAppId = args.BasicProperties.AppId;
                data.PublishDate = DateTime.Parse(Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["SendDate"]), CultureInfo.InvariantCulture);

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
                data.PublishDate = DateTime.Parse(Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["SendDate"]), CultureInfo.InvariantCulture);

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
                data.RequestDate = DateTime.Parse(Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["SendDate"]), CultureInfo.InvariantCulture);

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
                data.RequestDate = DateTime.Parse(Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["SendDate"]), CultureInfo.InvariantCulture);

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
                data.RequestDate = DateTime.Parse(Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["SendDate"]), CultureInfo.InvariantCulture);

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
            _cancellationTokenSource.Cancel();

            _sendTaskScheduler.Dispose();
            _receiveTaskScheduler.Dispose();
            _deadLetterQueueTaskScheduler.Dispose();

            _requestChannel.Dispose();
            _notifyChannel.Dispose();
            _responderChannel.Dispose();
            _eventHandlerChannel.Dispose();
            _deadLetterQueueChannel.Dispose();

            _connection.Dispose();

            _responseQueue.Dispose();

            if (_tracer != null)
                _tracer.Dispose();
        }
    }
}