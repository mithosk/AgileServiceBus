using AgileServiceBus.Enums;
using System;

namespace AgileServiceBus.Logging
{
    public class MessageDetail
    {
        public string Id { get; set; }
        public string CorrelationId { get; set; }
        public MessageType Type { get; set; }
        public string Directory { get; set; }
        public string Subdirectory { get; set; }
        public string Name { get; set; }
        public string Body { get; set; }
        public string AppId { get; set; }
        public Exception Exception { get; set; }
        public bool ToRetry { get; set; }
    }
}