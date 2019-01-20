using System.Threading.Tasks;

namespace AgileSB.Interfaces
{
    public interface IMicroserviceBus
    {
        Task NotifyAsync<TEvent>(TEvent message) where TEvent : class;
        Task NotifyAsync<TEvent>(TEvent message, string tag) where TEvent : class;
    }
}