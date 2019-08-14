using System;
using System.Linq;

namespace AgileServiceBus.Utilities
{
    public class ResponseWrapper<TResponse>
    {
        public TResponse Response { get; set; }
        public string ExceptionCode { get; set; }
        public string ExceptionMessage { get; set; }

        public ResponseWrapper() { }

        public ResponseWrapper(TResponse response)
        {
            Response = response;
        }

        public ResponseWrapper(Exception exception)
        {
            ExceptionCode = string.Concat(exception.GetType().Name.Select((cha, i) => i > 0 && char.IsUpper(cha) ? "_" + cha.ToString() : cha.ToString())).ToUpper();
            ExceptionMessage = exception.Message;
        }
    }
}