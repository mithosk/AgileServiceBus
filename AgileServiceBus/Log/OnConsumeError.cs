using System;

namespace PhotosiMessageLibrary.Log
{
    public class OnConsumeError
    {
        public string QueueName { get; set; }
        public string Message { get; set; }
        public string MessageId { get; set; }
        public Exception Exception { get; set; }
        public uint RetryIndex { get; set; }
        public ushort? RetryLimit { get; set; }
        public string PublisherAppId { get; set; }
        public DateTimeOffset PublishDate { get; set; }
    }
}