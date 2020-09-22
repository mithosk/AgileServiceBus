using System;
using System.Threading.Tasks;

namespace AgileServiceBus.Interfaces
{
    public interface ISchedulerBus : IDisposable
    {
        void Schedule<TEvent>(string cron, Func<TEvent> createMessage, Func<Exception, Task> error) where TEvent : class;
    }
}