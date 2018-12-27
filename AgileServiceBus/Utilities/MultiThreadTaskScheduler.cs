﻿using System;
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

        public MultiThreadTaskScheduler(byte numberOfThreads)
        {
            _cancellationTokenSource = new CancellationTokenSource();

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
        }

        protected override void QueueTask(Task task)
        {
            try
            {
                _tasks.Add(task, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException) { }
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            if (_cancellationTokenSource.Token.IsCancellationRequested)
                return new List<Task>();

            return _tasks.ToList();
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (_cancellationTokenSource.Token.IsCancellationRequested)
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
                foreach (Task task in _tasks.GetConsumingEnumerable(_cancellationTokenSource.Token))
                    TryExecuteTask(task);
            }
            catch (OperationCanceledException) { }
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
        }
    }
}