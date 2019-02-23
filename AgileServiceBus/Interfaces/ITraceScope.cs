using System;

namespace AgileServiceBus.Interfaces
{
    public interface ITraceScope : IDisposable
    {
        ITraceScope CreateSubScope(string displayName);
    }
}