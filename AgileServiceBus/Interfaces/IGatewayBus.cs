using System;
using System.Threading.Tasks;

namespace AgileServiceBus.Interfaces
{
    public interface IGatewayBus : IDisposable
    {
        Task<TResponse> RequestAsync<TResponse>(object message);
        Task<TResponse> RequestAsync<TResponse>(object message, ITraceScope traceScope);
    }
}