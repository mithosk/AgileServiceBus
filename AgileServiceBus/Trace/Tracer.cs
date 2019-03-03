using AgileServiceBus.Interfaces;
using AgileServiceBus.Utilities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AgileServiceBus.Trace
{
    public abstract class Tracer : IDisposable
    {
        private const byte NUMBER_OF_THREADS = 1;

        private MultiThreadTaskScheduler _taskScheduler;
        private CancellationTokenSource _cancellationTokenSource;

        public abstract Task TraceAsync(TraceSpan span);

        public Tracer()
        {
            _taskScheduler = new MultiThreadTaskScheduler(NUMBER_OF_THREADS);
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public ITraceScope CreateScope(string displayName)
        {
            return new TraceScope(displayName, this);
        }

        internal void Trace(TraceSpan span)
        {
            Task.Factory.StartNew(async () =>
            {
                try
                {
                    await TraceAsync(span);
                }
                catch { }
            },
            _cancellationTokenSource.Token,
            TaskCreationOptions.DenyChildAttach,
            _taskScheduler);
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _taskScheduler.Dispose();
        }
    }
}