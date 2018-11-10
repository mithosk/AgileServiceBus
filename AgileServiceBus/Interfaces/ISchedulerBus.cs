using System;
using System.Threading.Tasks;

namespace PhotosiMessageLibrary.Interfaces
{
    public interface ISchedulerBus : IDisposable
    {
        void Schedule<TMessage>(string cron, Func<TMessage> createMessage, Func<Exception, Task> onError) where TMessage : class;
    }
}