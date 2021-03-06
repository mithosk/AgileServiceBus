﻿using System;
using System.Linq;

namespace AgileServiceBus.Utilities
{
    public class ResponseWrapper<TResponse>
    {
        public TResponse Response { get; set; }
        public string ExceptionCode { get; set; }
        public string ExceptionMessage { get; set; }

        public ResponseWrapper()
        {
            Response = default;
            ExceptionCode = null;
            ExceptionMessage = null;
        }

        public ResponseWrapper(TResponse response)
        {
            Response = response;
            ExceptionCode = null;
            ExceptionMessage = null;
        }

        public ResponseWrapper(Exception exception)
        {
            Response = default;
            ExceptionCode = string.Concat(exception.GetType().Name.Replace("Exception", "").Select((cha, i) => i > 0 && char.IsUpper(cha) ? "_" + cha.ToString() : cha.ToString())).ToUpper();
            ExceptionMessage = exception.Message;
        }
    }
}