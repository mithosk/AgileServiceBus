using System.Threading.Tasks;

namespace PhotosiMessageLibrary.Interfaces
{
    public interface IRequestSubscriber<TRequest> where TRequest : class
    {
        IMicroserviceBus Bus { get; set; }

        Task<object> ResponseAsync(TRequest request);
    }
}