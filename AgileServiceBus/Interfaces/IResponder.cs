using System.Threading.Tasks;

namespace AgileServiceBus.Interfaces
{
    public interface IResponder<TRequest> where TRequest : class
    {
        IMicroserviceBus Bus { get; set; }
        ITraceScope TraceScope { get; set; }

        Task<object> RespondAsync(TRequest message);
    }
}