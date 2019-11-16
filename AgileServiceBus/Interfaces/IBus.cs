using AgileServiceBus.Interfaces;

namespace AgileSB.Interfaces
{
    public interface IBus : IGatewayBus, IMicroserviceBus, IMicroserviceLifetime, ISchedulerBus { }
}