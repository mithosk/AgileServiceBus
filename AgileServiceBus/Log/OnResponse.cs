using System;

namespace AgileSB.Log
{
    public class OnResponse
    {
        public string RequestQueueName { get; set; }
        public string Response { get; set; }
        public string CorrelationId { get; set; }
        public string RequesterAppId { get; set; }
        public DateTimeOffset RequestDate { get; set; }
    }
}