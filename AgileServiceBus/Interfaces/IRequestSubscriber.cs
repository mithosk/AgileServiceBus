using AgileServiceBus.Interfaces;
using System.Threading.Tasks;

namespace AgileSB.Interfaces
{
    public interface IRequestSubscriber<TRequest> where TRequest : class
    {
        IMicroserviceBus Bus { get; set; }
        ITraceScope TraceScope { get; set; }

        Task<object> ResponseAsync(TRequest request);
    }
}