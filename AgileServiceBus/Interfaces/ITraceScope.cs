using System;
using System.Collections.Generic;

namespace AgileServiceBus.Interfaces
{
    public interface ITraceScope : IDisposable
    {
        string SpanId { get; }
        string TraceId { get; }
        Dictionary<string, string> Attributes { get; }

        ITraceScope CreateSubScope(string displayName);
    }
}