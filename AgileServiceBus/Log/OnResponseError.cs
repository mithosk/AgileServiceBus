using System;

namespace PhotosiMessageLibrary.Log
{
    public class OnResponseError
    {
        public string RequestQueueName { get; set; }
        public string Request { get; set; }
        public string CorrelationId { get; set; }
        public Exception Exception { get; set; }
        public ushort RetryIndex { get; set; }
        public ushort RetryLimit { get; set; }
        public string RequesterAppId { get; set; }
        public DateTimeOffset RequestDate { get; set; }
    }
}