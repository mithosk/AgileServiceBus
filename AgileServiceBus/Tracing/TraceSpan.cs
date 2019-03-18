using System;
using System.Collections.Generic;

namespace AgileServiceBus.Tracing
{
    public class TraceSpan
    {
        public string Id { get; set; }
        public string ParentId { get; set; }
        public string TraceId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string DisplayName { get; set; }
        public Dictionary<string, string> Attributes { get; set; }
    }
}