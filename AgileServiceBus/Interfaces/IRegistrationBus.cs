using AgileServiceBus.Interfaces;
using AgileServiceBus.Logging;
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
        IExcludeForRetry Subscribe<TSubscriber, TEvent>(string tag, AbstractValidator<TEvent> validator, string retryCron, ushort? retryLimit) where TSubscriber : IEventHandler<TEvent> where TEvent : class;
        void RegisterLogger<TLogger>() where TLogger : Logger;
        void RegisterTracer<TTracer>() where TTracer : Tracer;
        void RegistrationCompleted();
    }
}