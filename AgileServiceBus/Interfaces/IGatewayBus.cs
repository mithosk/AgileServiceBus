using System;
using System.Threading.Tasks;

namespace AgileServiceBus.Interfaces
{
    public interface IGatewayBus : IDisposable
    {
        Task<TResponse> RequestAsync<TResponse>(object message);
        Task RequestAsync(object message);
        Task<TResponse> RequestAsync<TResponse>(object message, ITraceScope traceScope);
        Task RequestAsync(object message, ITraceScope traceScope);
    }
}