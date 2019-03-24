using AgileServiceBus.Interfaces;
using System;
using System.Collections.Generic;

namespace AgileServiceBus.Tracing
{
    public class TraceScope : ITraceScope
    {
        private string _parentSpanId;
        private DateTime _startTime;
        private string _displayName;
        private Tracer _tracer;

        public string SpanId { get; }
        public string TraceId { get; }
        public Dictionary<string, string> Attributes { get; }

        public TraceScope(string displayName, Tracer tracer)
        {
            SpanId = tracer.CreateSpanId();
            _parentSpanId = null;
            TraceId = tracer.CreateTraceId();
            _startTime = DateTime.UtcNow;
            _displayName = displayName;
            Attributes = new Dictionary<string, string>();

            _tracer = tracer;
        }

        public TraceScope(string parentSpanId, string traceId, string displayName, Tracer tracer)
        {
            SpanId = tracer.CreateSpanId();
            _parentSpanId = parentSpanId;
            TraceId = traceId;
            _startTime = DateTime.UtcNow;
            _displayName = displayName;
            Attributes = new Dictionary<string, string>();

            _tracer = tracer;
        }

        public ITraceScope CreateSubScope(string displayName)
        {
            return new TraceScope(SpanId, TraceId, displayName, _tracer);
        }

        public void Dispose()
        {
            TraceSpan traceSpan = new TraceSpan();
            traceSpan.Id = SpanId;
            traceSpan.ParentId = _parentSpanId;
            traceSpan.TraceId = TraceId;
            traceSpan.StartTime = _startTime;
            traceSpan.EndTime = DateTime.UtcNow;
            traceSpan.DisplayName = _displayName;
            traceSpan.Attributes = Attributes;

            _tracer.Send(traceSpan);
        }
    }
}