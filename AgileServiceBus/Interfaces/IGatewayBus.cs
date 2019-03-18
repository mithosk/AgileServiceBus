using AgileServiceBus.Interfaces;
using System;
using System.Threading.Tasks;

namespace AgileSB.Interfaces
{
    public interface IGatewayBus : IDisposable
    {
        Task<TResponse> RequestAsync<TResponse>(object request);
        Task<TResponse> RequestAsync<TResponse>(object request, ITraceScope traceScope);
    }
}