using AgileServiceBus.Interfaces;
using AgileServiceBus.Tracing;
using Autofac;
using FluentValidation;
using System;

namespace AgileSB.Interfaces
{
    public interface IRegistrationBus : IDisposable
    {
        ILogger Logger { set; }
        ContainerBuilder Container { get; }

        IIncludeForRetry Subscribe<TSubscriber, TRequest>(AbstractValidator<TRequest> validator) where TSubscriber : IResponder<TRequest> where TRequest : class;
        IExcludeForRetry Subscribe<TSubscriber, TMessage>(string tag, AbstractValidator<TMessage> validator, string retryCron, ushort? retryLimit) where TSubscriber : IPublishSubscriber<TMessage> where TMessage : class;
        void RegisterTracer<TTracer>() where TTracer : Tracer;
        void RegistrationCompleted();
    }
}