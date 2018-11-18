using System;

namespace AgileServiceBus.Interfaces
{
    public interface IIncludeForRetry
    {
        IIncludeForRetry IncludeForRetry<TException>() where TException : Exception;
    }
}