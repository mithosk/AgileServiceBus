using System;
using System.Threading.Tasks;

namespace AgileServiceBus.Interfaces
{
    public interface IRetry : IExcludeForRetry, IIncludeForRetry
    {
        bool IsForRetry<TException>(TException exception) where TException : Exception;
        Task ExecuteAsync(Func<Task> toRetry, Func<Exception, ushort, ushort, Task> error);
    }
}