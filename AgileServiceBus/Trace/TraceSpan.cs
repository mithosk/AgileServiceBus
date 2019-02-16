using System;
using System.Collections.Generic;

namespace AgileServiceBus.Trace
{
    public class TraceSpan
    {
        public Guid Id { get; set; }
        public Guid? ParentId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string DisplayName { get; set; }
        public Dictionary<string, string> Attributes { get; set; }
    }
}