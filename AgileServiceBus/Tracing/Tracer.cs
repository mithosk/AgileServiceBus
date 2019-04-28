using AgileServiceBus.Interfaces;
using AgileServiceBus.Utilities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AgileServiceBus.Tracing
{
    public abstract class Tracer : IDisposable
    {
        private const byte NUMBER_OF_THREADS = 1;

        private MultiThreadTaskScheduler _taskScheduler;
        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _cancellationToken;

        public abstract string CreateTraceId();
        public abstract string CreateSpanId();
        public abstract Task SendAsync(TraceSpan traceSpan);

        public Tracer()
        {
            _taskScheduler = new MultiThreadTaskScheduler(NUMBER_OF_THREADS);
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;
        }

        public ITraceScope CreateScope(string displayName)
        {
            return new TraceScope(displayName, this);
        }

        internal void Send(TraceSpan traceSpan)
        {
            Task.Factory.StartNew(async () =>
            {
                try
                {
                    await SendAsync(traceSpan);
                }
                catch { }
            },
            _cancellationToken,
            TaskCreationOptions.DenyChildAttach,
            _taskScheduler);
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _taskScheduler.Dispose();
        }
    }
}