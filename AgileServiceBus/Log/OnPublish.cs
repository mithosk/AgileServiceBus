using System;

namespace AgileSB.Log
{
    public class OnPublish
    {
        public string QueueName { get; set; }
        public string Message { get; set; }
        public string MessageId { get; set; }
        public string PublisherAppId { get; set; }
        public DateTimeOffset PublishDate { get; set; }
    }
}