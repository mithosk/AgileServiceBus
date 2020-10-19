using System;

namespace AgileServiceBus.Exceptions
{
    public class NamingException : Exception
    {
        public NamingException(string message) : base(message) { }
    }
}