using AgileServiceBus.Interfaces;
using System;
using Topshelf;

namespace AgileServiceBus.Additionals
{
    public class ServiceFactory
    {
        public static void Run(Func<IMicroserviceLifetime> configurator)
        {
            HostFactory.Run(hco =>
            {
                hco.Service<Service>(sce =>
                {
                    sce.ConstructUsing(hso => new Service());
                    sce.WhenStarted(ser => ser.Start(configurator));
                    sce.WhenStopped(ser => ser.Stop());
                });

                hco.RunAsLocalService();
            });
        }

        private class Service
        {
            private IMicroserviceLifetime _ml;

            public Service()
            {
                _ml = null;
            }

            public void Start(Func<IMicroserviceLifetime> configurator)
            {
                _ml = configurator();
            }

            public void Stop()
            {
                _ml.Dispose();

                _ml = null;

                GC.Collect();
            }
        }
    }
}