using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AgileServiceBus.Utilities
{
    public class MultiThreadTaskScheduler : TaskScheduler, IDisposable
    {
        private BlockingCollection<Task> _tasks;
        private Thread[] _threads;
        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _cancellationToken;

        public MultiThreadTaskScheduler(byte numberOfThreads)
        {
            _tasks = new BlockingCollection<Task>();

            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;

            _threads = new Thread[numberOfThreads];
            for (byte i = 0; i < numberOfThreads; i++)
            {
                _threads[i] = new Thread(ExecuteTasks);
                _threads[i].IsBackground = true;
                _threads[i].SetApartmentState(ApartmentState.MTA);
                _threads[i].Priority = ThreadPriority.Normal;
                _threads[i].Start();
            }
        }

        protected override void QueueTask(Task task)
        {
            try
            {
                _tasks.Add(task, _cancellationToken);
            }
            catch { }
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            try
            {
                return _tasks.ToList();
            }
            catch
            {
                return new List<Task>();
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (_cancellationToken.IsCancellationRequested)
                return false;

            if (!_threads.Contains(Thread.CurrentThread))
                return false;

            TryExecuteTask(task);
            return true;
        }

        private void ExecuteTasks()
        {
            try
            {
                foreach (Task task in _tasks.GetConsumingEnumerable(_cancellationToken))
                    TryExecuteTask(task);
            }
            catch { }
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _tasks.Dispose();
        }
    }
}