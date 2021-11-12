using AgileServiceBus.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AgileServiceBus.Additionals
{
    public class HostFactory
    {
        public static void Run(Func<IMicroserviceLifetime> configure)
        {
            Host.CreateDefaultBuilder()
                .ConfigureServices((hostBuilderContext, serviceCollection) =>
                {
                    serviceCollection.AddHostedService(spe => new HostedMicroservice(configure));
                })
                .Build()
                .Run();
        }

        public static void Run(Func<ISchedulerBus> configure)
        {
            Host.CreateDefaultBuilder()
                .ConfigureServices((hostBuilderContext, serviceCollection) =>
                {
                    serviceCollection.AddHostedService(spe => new HostedScheduler(configure));
                })
                .Build()
                .Run();
        }

        private class HostedMicroservice : IHostedService
        {
            private IMicroserviceLifetime _microserviceLifetime;
            private readonly Func<IMicroserviceLifetime> _configure;

            public HostedMicroservice(Func<IMicroserviceLifetime> configure)
            {
                _microserviceLifetime = null;
                _configure = configure;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                _microserviceLifetime = _configure();
                GC.Collect();
                _microserviceLifetime.Startup();

                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                _microserviceLifetime.Dispose();
                _microserviceLifetime = null;
                GC.Collect();

                return Task.CompletedTask;
            }
        }

        private class HostedScheduler : IHostedService
        {
            private ISchedulerBus _schedulerBus;
            private readonly Func<ISchedulerBus> _configure;

            public HostedScheduler(Func<ISchedulerBus> configure)
            {
                _schedulerBus = null;
                _configure = configure;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                _schedulerBus = _configure();
                GC.Collect();

                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                _schedulerBus.Dispose();
                _schedulerBus = null;
                GC.Collect();

                return Task.CompletedTask;
            }
        }
    }
}