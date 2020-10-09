using System;

namespace AgileServiceBus.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class BusNamespace : Attribute
    {
        public string Directory { get; set; }
        public string Subdirectory { get; set; }
    }
}