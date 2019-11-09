using AgileServiceBus.Utilities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AgileServiceBus.Logging
{
    public abstract class Logger : IDisposable
    {
        private const byte NUMBER_OF_THREADS = 1;

        private MultiThreadTaskScheduler _taskScheduler;
        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _cancellationToken;

        public abstract Task SendAsync(MessageDetail messageDetail);

        public Logger()
        {
            _taskScheduler = new MultiThreadTaskScheduler(NUMBER_OF_THREADS);
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;
        }

        internal void Send(MessageDetail messageDetail)
        {
            Task.Factory.StartNew(async () =>
            {
                try
                {
                    await SendAsync(messageDetail);
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