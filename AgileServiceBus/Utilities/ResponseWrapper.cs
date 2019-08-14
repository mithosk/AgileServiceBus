using System;
using System.Linq;

namespace AgileServiceBus.Utilities
{
    public class ResponseWrapper<TResponse>
    {
        public TResponse Response { get; private set; }
        public string ExceptionCode { get; private set; }
        public string ExceptionMessage { get; private set; }

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