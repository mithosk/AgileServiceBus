﻿using System;

namespace PhotosiMessageLibrary.Exceptions
{
    public class RemoteException : Exception
    {
        public string Code { get; private set; }

        public RemoteException(string code, string message) : base(message)
        {
            Code = code;
        }
    }
}