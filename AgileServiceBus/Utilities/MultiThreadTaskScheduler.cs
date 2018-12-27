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
        private bool _disposed;

        public MultiThreadTaskScheduler(byte numberOfThreads)
        {
            _tasks = new BlockingCollection<Task>();

            _threads = new Thread[numberOfThreads];
            for (byte i = 0; i < numberOfThreads; i++)
            {
                _threads[i] = new Thread(ExecuteTasks);
                _threads[i].IsBackground = true;
                _threads[i].SetApartmentState(ApartmentState.MTA);
                _threads[i].Priority = ThreadPriority.Normal;
                _threads[i].Start();
            }

            _disposed = false;
        }

        protected override void QueueTask(Task task)
        {
            try
            {
                _tasks.Add(task);
            }
            catch (ObjectDisposedException) { }
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            if (_disposed)
                return new List<Task>();

            return _tasks.ToList();
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (_disposed)
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
                foreach (Task task in _tasks.GetConsumingEnumerable())
                    TryExecuteTask(task);
            }
            catch (ObjectDisposedException) { }
        }

        public void Dispose()
        {
            _disposed = true;

            _tasks.Dispose();
        }
    }
}