using System.Threading.Tasks;

namespace PhotosiMessageLibrary.Interfaces
{
    public interface IPublishSubscriber<TMessage> where TMessage : class
    {
        IMicroserviceBus Bus { get; set; }

        Task ConsumeAsync(TMessage message);
    }
}