using System;

namespace AgileServiceBus.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class FakeResponse : Attribute { }
}