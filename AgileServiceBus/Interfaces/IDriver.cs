namespace AgileServiceBus.Interfaces
{
    public interface IDriver : IGatewayBus, IMicroserviceBus, IMicroserviceLifetime, ISchedulerBus { }
}