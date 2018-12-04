using AgileServiceBus.Interfaces;
using Autofac;
using System;

namespace AgileSB.Interfaces
{
    public interface IRegistrationBus : IDisposable
    {
        ILogger Logger { set; }
        ContainerBuilder Container { get; }

        IIncludeForRetry Subscribe<TSubscriber, TRequest>() where TSubscriber : IRequestSubscriber<TRequest> where TRequest : class;
        IExcludeForRetry Subscribe<TSubscriber, TMessage>(string topic, ushort prefetchCount, string retryCron, ushort? retryLimit) where TSubscriber : IPublishSubscriber<TMessage> where TMessage : class;
        void RegistrationCompleted();
    }
}