using AgileServiceBus.Interfaces;

namespace AgileSB.Interfaces
{
    public interface IDriver : IGatewayBus, IMicroserviceBus, IMicroserviceLifetime, ISchedulerBus { }
}