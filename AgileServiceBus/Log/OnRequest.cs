using System;

namespace AgileSB.Log
{
    public class OnRequest
    {
        public string QueueName { get; set; }
        public string Request { get; set; }
        public string CorrelationId { get; set; }
        public string RequesterAppId { get; set; }
        public DateTimeOffset RequestDate { get; set; }
    }
}