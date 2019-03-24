﻿using AgileServiceBus.Interfaces;
using System.Threading.Tasks;

namespace AgileSB.Interfaces
{
    public interface IPublishSubscriber<TMessage> where TMessage : class
    {
        IMicroserviceBus Bus { get; set; }
        ITraceScope TraceScope { get; set; }

        Task ConsumeAsync(TMessage message);
    }
}