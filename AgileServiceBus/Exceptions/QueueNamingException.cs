using System;

namespace AgileSB.Exceptions
{
    public class QueueNamingException : Exception
    {
        public QueueNamingException(string message) : base(message) { }
    }
}