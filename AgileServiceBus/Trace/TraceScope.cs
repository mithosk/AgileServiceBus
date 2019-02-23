using AgileServiceBus.Interfaces;
using System;
using System.Collections.Generic;

namespace AgileServiceBus.Trace
{
    public class TraceScope : ITraceScope
    {
        private Guid? _parentSpanId;
        private DateTime _startTime;
        private string _displayName;
        private Tracer _tracer;

        public Guid SpanId { get; private set; }
        public Dictionary<string, string> Attributes { get; private set; }

        public TraceScope(string displayName, Tracer tracer)
        {
            SpanId = Guid.NewGuid();
            _parentSpanId = null;
            _startTime = DateTime.UtcNow;
            _displayName = displayName;
            Attributes = new Dictionary<string, string>();

            _tracer = tracer;
        }

        public TraceScope(Guid parentSpanId, string displayName, Tracer tracer)
        {
            SpanId = Guid.NewGuid();
            _parentSpanId = parentSpanId;
            _startTime = DateTime.UtcNow;
            _displayName = displayName;
            Attributes = new Dictionary<string, string>();

            _tracer = tracer;
        }

        public ITraceScope CreateSubScope(string displayName)
        {
            return new TraceScope(SpanId, displayName, _tracer);
        }

        public void Dispose()
        {
            TraceSpan span = new TraceSpan();
            span.Id = SpanId;
            span.ParentId = _parentSpanId;
            span.StartTime = _startTime;
            span.EndTime = DateTime.UtcNow;
            span.DisplayName = _displayName;
            span.Attributes = Attributes;

            _tracer.Trace(span);
        }
    }
}