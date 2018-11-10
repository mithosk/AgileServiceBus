using System.Threading.Tasks;

namespace PhotosiMessageLibrary.Interfaces
{
    public interface IMicroserviceBus
    {
        Task PublishAsync<TMessage>(TMessage message) where TMessage : class;
        Task PublishAsync<TMessage>(TMessage message, string topic) where TMessage : class;
    }
}