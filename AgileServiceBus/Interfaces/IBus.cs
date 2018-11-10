namespace PhotosiMessageLibrary.Interfaces
{
    public interface IBus : IGatewayBus, IMicroserviceBus, IRegistrationBus, ISchedulerBus { }
}