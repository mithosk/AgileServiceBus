using System;
using System.Threading.Tasks;

namespace PhotosiMessageLibrary.Interfaces
{
    public interface IGatewayBus : IDisposable
    {
        Task<TResponse> RequestAsync<TResponse>(object request);
    }
}