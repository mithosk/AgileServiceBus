using System;

namespace AgileServiceBus.Interfaces
{
    public interface IExcludeForRetry
    {
        IExcludeForRetry ExcludeForRetry<TException>() where TException : Exception;
    }
}