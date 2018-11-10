using Autofac;
using System;

namespace PhotosiMessageLibrary.Interfaces
{
    public interface IRegistrationBus : IDisposable
    {
        ILogger Logger { set; }
        ContainerBuilder Container { get; }

        void Subscribe<TSubscriber, TRequest>(bool retry) where TSubscriber : IRequestSubscriber<TRequest> where TRequest : class;
        void Subscribe<TSubscriber, TMessage>(string topic, ushort prefetchCount, ushort? retryLimit, TimeSpan? retryDelay) where TSubscriber : IPublishSubscriber<TMessage> where TMessage : class;
        void RegistrationCompleted();
    }
}