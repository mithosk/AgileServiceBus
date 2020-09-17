using AgileSB.Attributes;
using AgileSB.Exceptions;
using AgileSB.Extensions;
using AgileSB.Interfaces;
using AgileServiceBus.Enums;
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
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AgileSB.Drivers
{
    public class RabbitMQDriver : IDriver
    {
        private const ushort RESPONDER_PREFETCHCOUNT = 30;
        private const ushort EVENT_HANDLER_PREFETCHCOUNT = 8;
        private const ushort REQUEST_TIMEOUT = 15000;
        private const int DEAD_LETTER_QUEUE_RECOVERY_LIMIT = 1000;
        private const ushort MIN_RETRY_DELAY = 1;
        private const ushort MAX_RETRY_DELAY = 250;
        private const ushort RETRY_LIMIT = 5;
        private const string DEAD_LETTER_QUEUE_EXCHANGE = "dead_letter_queue";
        private const string DIRECT_REPLY_QUEUE = "amq.rabbitmq.reply-to";
        private const byte SEND_NUMBER_OF_THREADS = 4;
        private const byte RECEIVE_NUMBER_OF_THREADS = 16;
        private const byte DEAD_LETTER_QUEUE_NUMBER_OF_THREADS = 1;
        private const ushort CACHE_LIMIT = 5000;
        private readonly TimeSpan CACHE_DURATION = new TimeSpan(2, 30, 0);

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
        private CacheHandler _cacheHandler;

        public ContainerBuilder Injection { get; }

        public RabbitMQDriver(string connectionString)
        {
            Dictionary<string, string> settings = connectionString.ParseAsConnectionString();

            //creates the connection
            ConnectionFactory connectionFactory = new ConnectionFactory();
            connectionFactory.HostName = settings["Host"];
            connectionFactory.VirtualHost = settings["VHost"];
            connectionFactory.Port = int.Parse(settings["Port"]);
            connectionFactory.UserName = settings["User"];
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
            Injection = new ContainerBuilder();

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

            //cache handler
            _cacheHandler = new CacheHandler(CACHE_LIMIT, CACHE_DURATION);
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
            return await RequestAsync<TResponse>(request, "", "");
        }

        public async Task RequestAsync(object message)
        {
            await RequestAsync<object>(message);
        }

        public async Task<TResponse> RequestAsync<TResponse>(object message, ITraceScope traceScope)
        {
            string directory = message.GetType().GetCustomAttribute<QueueConfig>().Directory;
            string subdirectory = message.GetType().GetCustomAttribute<QueueConfig>().Subdirectory;

            using (ITraceScope traceSubScope = traceScope.CreateSubScope("Request-" + directory + "." + subdirectory + "." + message.GetType().Name))
                return await RequestAsync<TResponse>(message, traceSubScope.SpanId, traceSubScope.TraceId);
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
            Injection.RegisterType<TSubscriber>().InstancePerLifetimeScope();

            //retry handler
            IRetry retryHandler = new RetryHandler(MIN_RETRY_DELAY, MAX_RETRY_DELAY, RETRY_LIMIT, true);

            //creates queue and exchange
            string directory = typeof(TRequest).GetTypeInfo().GetCustomAttribute<QueueConfig>().Directory;
            string subdirectory = typeof(TRequest).GetTypeInfo().GetCustomAttribute<QueueConfig>().Subdirectory;
            string exchange = "request_" + directory.ToLower() + "_" + subdirectory.ToLower();
            string routingKey = typeof(TRequest).Name.ToLower();
            string queue = _appId.ToLower() + "-request-" + directory.ToLower() + "-" + subdirectory.ToLower() + "-" + typeof(TRequest).Name.ToLower();
            _responderChannel.ExchangeDeclare(exchange, ExchangeType.Direct, true, false);
            _responderChannel.QueueDeclare(queue, true, false, false, new Dictionary<string, object> { { "x-message-ttl", (int)REQUEST_TIMEOUT }, { "x-queue-mode", "default" } });
            _responderChannel.QueueBind(queue, exchange, routingKey);

            //request listener
            EventingBasicConsumer consumer = new EventingBasicConsumer(_responderChannel);
            consumer.Received += (obj, args) =>
            {
                Task.Factory.StartNew(async () =>
                {
                    //request message
                    string messageBody = Encoding.UTF8.GetString(args.Body);

                    //tracing data
                    string traceSpanId = Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["TraceSpanId"]);
                    string traceId = Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["TraceId"]);
                    string traceDisplayName = "Respond-" + directory + "." + subdirectory + "." + typeof(TRequest).Name;

                    //response action
                    ResponseWrapper<object> responseWrapper = null;

                    await retryHandler.ExecuteAsync(async () =>
                    {
                        TRequest messageRequest = _jsonConverter.Deserialize<TRequest>(messageBody);
                        if (validator != null)
                            await validator.ValidateAndThrowAsync(messageRequest, (directory + "." + subdirectory + "." + messageRequest.GetType().Name + " is not valid"));

                        using (ILifetimeScope container = _container.BeginLifetimeScope())
                        using (ITraceScope traceScope = (traceSpanId != "" && traceId != "") ? new TraceScope(traceSpanId, traceId, traceDisplayName, _tracer) : new TraceScope(traceDisplayName, _tracer))
                        {
                            TSubscriber subscriber = container.Resolve<TSubscriber>();
                            subscriber.Bus = this;
                            subscriber.TraceScope = traceScope;
                            traceScope.Attributes.Add("AppId", _appId);
                            traceScope.Attributes.Add("MessageId", args.BasicProperties.MessageId);
                            responseWrapper = new ResponseWrapper<object>(await subscriber.RespondAsync(messageRequest));
                        }

                        _logger.Send(new MessageDetail
                        {
                            Id = args.BasicProperties.MessageId,
                            CorrelationId = args.BasicProperties.CorrelationId,
                            Type = MessageType.Request,
                            Directory = directory,
                            Subdirectory = subdirectory,
                            Name = typeof(TRequest).Name,
                            Body = messageBody,
                            AppId = args.BasicProperties.AppId,
                            Exception = null,
                            ToRetry = false
                        });
                    },
                    async (exception, retryIndex, retryLimit) =>
                    {
                        responseWrapper = new ResponseWrapper<object>(exception);

                        _logger.Send(new MessageDetail
                        {
                            Id = args.BasicProperties.MessageId,
                            CorrelationId = args.BasicProperties.CorrelationId,
                            Type = MessageType.Request,
                            Directory = directory,
                            Subdirectory = subdirectory,
                            Name = typeof(TRequest).Name,
                            Body = messageBody,
                            AppId = args.BasicProperties.AppId,
                            Exception = exception,
                            ToRetry = retryIndex != retryLimit
                        });

                        await Task.CompletedTask;
                    });

                    //response message
                    if (typeof(TSubscriber).GetMethod("RespondAsync").GetCustomAttribute<FakeResponse>() == null)
                    {
                        messageBody = _jsonConverter.Serialize(responseWrapper);
                        IBasicProperties properties = _responderChannel.CreateBasicProperties();
                        properties.MessageId = Guid.NewGuid().ToString();
                        properties.Persistent = false;
                        properties.CorrelationId = args.BasicProperties.CorrelationId;
                        _responderChannel.BasicPublish("", args.BasicProperties.ReplyTo, properties, Encoding.UTF8.GetBytes(messageBody));

                        _logger.Send(new MessageDetail
                        {
                            Id = properties.MessageId,
                            CorrelationId = properties.CorrelationId,
                            Type = MessageType.Response,
                            Directory = directory,
                            Subdirectory = subdirectory,
                            Name = typeof(TRequest).Name,
                            Body = messageBody,
                            AppId = args.BasicProperties.AppId,
                            Exception = null,
                            ToRetry = false
                        });
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
            Injection.RegisterType<TSubscriber>().InstancePerLifetimeScope();

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
            _eventHandlerChannel.QueueDeclare(queue, true, false, false, new Dictionary<string, object> { { "x-queue-mode", "lazy" } });
            _eventHandlerChannel.QueueBind(queue, exchange, routingKey);
            _eventHandlerChannel.QueueBind(queue, DEAD_LETTER_QUEUE_EXCHANGE, restoreRoutingKey);

            //creates dead letter queue
            string deadLetterQueue = (queue + "-dlq");
            string dlqRoutingKey = (_appId.ToLower() + "." + directory.ToLower() + "." + subdirectory.ToLower() + "." + typeof(TEvent).Name.ToLower() + (tag != null ? ("." + tag.ToLower()) : "") + ".dlq");
            _deadLetterQueueChannel.QueueDeclare(deadLetterQueue, true, false, false, new Dictionary<string, object> { { "x-queue-mode", "lazy" } });
            _deadLetterQueueChannel.QueueBind(deadLetterQueue, DEAD_LETTER_QUEUE_EXCHANGE, dlqRoutingKey);

            //message listener
            EventingBasicConsumer consumer = new EventingBasicConsumer(_eventHandlerChannel);
            consumer.Received += (obj, args) =>
            {
                Task.Factory.StartNew(async () =>
                {
                    string messageBody = Encoding.UTF8.GetString(args.Body);

                    try
                    {
                        TEvent messageEvent = _jsonConverter.Deserialize<TEvent>(messageBody);
                        if (validator != null)
                            await validator.ValidateAndThrowAsync(messageEvent, (directory + "." + subdirectory + "." + typeof(TEvent).Name + " is not valid"));

                        using (ILifetimeScope container = _container.BeginLifetimeScope())
                        using (ITraceScope traceScope = new TraceScope("Handle-" + directory + "." + subdirectory + "." + typeof(TEvent).Name, _tracer))
                        {
                            TSubscriber subscriber = container.Resolve<TSubscriber>();
                            subscriber.Bus = this;
                            subscriber.TraceScope = traceScope;
                            traceScope.Attributes.Add("AppId", _appId);
                            traceScope.Attributes.Add("MessageId", args.BasicProperties.MessageId);
                            await subscriber.HandleAsync(messageEvent);
                        }

                        _logger.Send(new MessageDetail
                        {
                            Id = args.BasicProperties.MessageId,
                            CorrelationId = null,
                            Type = MessageType.Event,
                            Directory = directory,
                            Subdirectory = subdirectory,
                            Name = typeof(TEvent).Name,
                            Body = messageBody,
                            AppId = args.BasicProperties.AppId,
                            Exception = null,
                            ToRetry = false
                        });
                    }
                    catch (Exception exception)
                    {
                        bool toRetry = false;
                        ushort retryIndex = ushort.Parse(Encoding.UTF8.GetString((byte[])args.BasicProperties.Headers["RetryIndex"]));
                        if (retryHandler.IsForRetry(exception) && !string.IsNullOrEmpty(retryCron) && retryLimit != null && retryIndex < retryLimit)
                        {
                            IBasicProperties properties = _deadLetterQueueChannel.CreateBasicProperties();
                            properties.MessageId = args.BasicProperties.MessageId;
                            properties.AppId = args.BasicProperties.AppId;
                            properties.Headers = new Dictionary<string, object>();
                            properties.Headers.Add("RetryIndex", (++retryIndex).ToString());
                            properties.Persistent = true;
                            _deadLetterQueueChannel.BasicPublish(DEAD_LETTER_QUEUE_EXCHANGE, dlqRoutingKey, properties, args.Body);

                            toRetry = true;
                        }

                        _logger.Send(new MessageDetail
                        {
                            Id = args.BasicProperties.MessageId,
                            CorrelationId = null,
                            Type = MessageType.Event,
                            Directory = directory,
                            Subdirectory = subdirectory,
                            Name = typeof(TEvent).Name,
                            Body = messageBody,
                            AppId = args.BasicProperties.AppId,
                            Exception = exception,
                            ToRetry = toRetry
                        });
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

        public void Startup()
        {
            _container = Injection.Build();

            _logger = (Logger)Activator.CreateInstance(_loggerType);
            _tracer = (Tracer)Activator.CreateInstance(_tracerType);

            foreach (Tuple<IModel, string, EventingBasicConsumer> toActivateConsumer in _toActivateConsumers)
                toActivateConsumer.Item1.BasicConsume(toActivateConsumer.Item2, false, toActivateConsumer.Item3);
        }

        private async Task<TResponse> RequestAsync<TResponse>(object request, string traceSpanId, string traceId)
        {
            //get from cache
            ICacheId cacheId = request as ICacheId;
            if (cacheId != null)
            {
                TResponse cached = _cacheHandler.Get<TResponse>(cacheId);
                if (cached != null && !cached.Equals(default(TResponse)))
                    return cached;
            }

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
                string messageBody = _responseQueue.WaitMessage(correlationId);
                if (messageBody != null)
                    responseWrapper = _jsonConverter.Deserialize<ResponseWrapper<TResponse>>(messageBody);

                _responseQueue.RemoveGroup(correlationId);
            },
            _cancellationTokenSource.Token,
            TaskCreationOptions.DenyChildAttach,
            _receiveTaskScheduler);

            //timeout
            if (responseWrapper == null)
                throw new TimeoutException(request.GetType().Name + " did Not Respond");

            //remote error
            if (responseWrapper.ExceptionCode != null)
                throw new RemoteException(responseWrapper.ExceptionCode, responseWrapper.ExceptionMessage);

            //add to cache
            if (cacheId != null)
                _cacheHandler.Set(cacheId, responseWrapper.Response);

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

            if (_logger != null)
                _logger.Dispose();

            if (_tracer != null)
                _tracer.Dispose();
        }
    }
}