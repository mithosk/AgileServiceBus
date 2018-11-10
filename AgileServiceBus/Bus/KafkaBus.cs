using Autofac;
using PhotosiMessageLibrary.Interfaces;
using System;
using System.Threading.Tasks;

namespace PhotosiMessageLibrary.Bus
{
    public class KafkaBus : IBus
    {
        public ILogger Logger { get; set; }
        public ContainerBuilder Container { get; }

        public KafkaBus(string connectionString) { }

        public async Task<TResponse> RequestAsync<TResponse>(object request)
        {
            await Task.Delay(1000);
            return (default(TResponse));
        }

        public async Task PublishAsync<TMessage>(TMessage message) where TMessage : class
        {
            await PublishAsync(message, null);
        }

        public async Task PublishAsync<TMessage>(TMessage message, string topic) where TMessage : class
        {
            await Task.Delay(1000);
        }

        public void Subscribe<TSubscriber, TRequest>(bool retry) where TSubscriber : IRequestSubscriber<TRequest> where TRequest : class { }

        public void Subscribe<TSubscriber, TMessage>(string topic, ushort prefetchCount, ushort? retryLimit, TimeSpan? retryDelay) where TSubscriber : IPublishSubscriber<TMessage> where TMessage : class { }

        public void Schedule<TMessage>(string cron, Func<TMessage> createMessage, Func<Exception, Task> onError) where TMessage : class { }

        public void RegistrationCompleted() { }

        public void Dispose() { }
    }
}