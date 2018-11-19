using System;
using System.Threading.Tasks;

namespace AgileServiceBus.Interfaces
{
    public interface IRetry : IExcludeForRetry, IIncludeForRetry
    {
        bool IsForRetry(Exception exception);
        Task ExecuteAsync(Func<Task> toRetry, Func<Exception, ushort, ushort, Task> error);
    }
}