using System.Threading.Tasks;

namespace AgileServiceBus.Interfaces
{
    public interface IEventHandler<TEvent> where TEvent : class
    {
        IMicroserviceBus Bus { get; set; }
        ITraceScope TraceScope { get; set; }

        Task HandleAsync(TEvent message);
    }
}