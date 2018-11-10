using System;

namespace PhotosiMessageLibrary.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class QueueConfig : Attribute
    {
        public string Directory { get; set; }
        public string Subdirectory { get; set; }
    }
}