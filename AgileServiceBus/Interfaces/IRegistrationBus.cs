using AgileServiceBus.Interfaces;
using AgileServiceBus.Trace;
using Autofac;
using FluentValidation;
using System;

namespace AgileSB.Interfaces
{
    public interface IRegistrationBus : IDisposable
    {
        ILogger Logger { set; }
        ContainerBuilder Container { get; }

        IIncludeForRetry Subscribe<TSubscriber, TRequest>(AbstractValidator<TRequest> validator) where TSubscriber : IRequestSubscriber<TRequest> where TRequest : class;
        IExcludeForRetry Subscribe<TSubscriber, TMessage>(string topic, ushort prefetchCount, AbstractValidator<TMessage> validator, string retryCron, ushort? retryLimit) where TSubscriber : IPublishSubscriber<TMessage> where TMessage : class;
        void RegisterTracer<TTracer>() where TTracer : Tracer;
        void RegistrationCompleted();
    }
}