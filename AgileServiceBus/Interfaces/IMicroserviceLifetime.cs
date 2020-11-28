using AgileServiceBus.Logging;
using AgileServiceBus.Tracing;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace AgileServiceBus.Interfaces
{
    public interface IMicroserviceLifetime : IDisposable
    {
        IServiceCollection Injection { get; }

        IIncludeForRetry Subscribe<TResponder, TRequest>(AbstractValidator<TRequest> validator) where TResponder : IResponder<TRequest> where TRequest : class;
        IExcludeForRetry Subscribe<TEventHandler, TEvent>(string tag, AbstractValidator<TEvent> validator, string retryCron, ushort? retryLimit) where TEventHandler : IEventHandler<TEvent> where TEvent : class;
        void RegisterLogger<TLogger>() where TLogger : Logger;
        void RegisterTracer<TTracer>() where TTracer : Tracer;
        void Startup();
    }
}