using System;

namespace PhotosiMessageLibrary.Log
{
    public class OnConsumed
    {
        public string QueueName { get; set; }
        public string Message { get; set; }
        public string MessageId { get; set; }
        public string PublisherAppId { get; set; }
        public DateTimeOffset PublishDate { get; set; }
    }
}