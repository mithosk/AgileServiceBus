using System;
using System.Collections.Generic;

namespace AgileServiceBus.Interfaces
{
    public interface ITraceScope : IDisposable
    {
        Guid SpanId { get; }
        Dictionary<string, string> Attributes { get; }

        ITraceScope CreateSubScope(string displayName);
    }
}