using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;

namespace AgileServiceBus.Utilities
{
    public class MessageGroupQueue : IDisposable
    {
        private Hashtable _queues;
        private ushort _timeout;
        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _cancellationToken;

        public MessageGroupQueue(ushort timeout)
        {
            _queues = new Hashtable();
            _timeout = timeout;
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;
        }

        public void AddGroup(string groupId)
        {
            if (!_cancellationToken.IsCancellationRequested)
                _queues.Add(groupId, new BlockingCollection<string>());
        }

        public void RemoveGroup(string groupId)
        {
            BlockingCollection<string> queue = (BlockingCollection<string>)_queues[groupId];

            _queues.Remove(groupId);

            if (queue != null)
                queue.Dispose();
        }

        public void AddMessage(string message, string groupId)
        {
            BlockingCollection<string> queue = (BlockingCollection<string>)_queues[groupId];

            try
            {
                if (queue != null)
                    queue.Add(message, _cancellationToken);
            }
            catch { }
        }

        public string WaitMessage(string groupId)
        {
            string message = null;

            BlockingCollection<string> queue = (BlockingCollection<string>)_queues[groupId];

            try
            {
                if (queue != null)
                    queue.TryTake(out message, _timeout, _cancellationToken);
            }
            catch { }

            return message;
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();

            foreach (BlockingCollection<string> queue in _queues.Values)
                queue.Dispose();

            _queues.Clear();
        }
    }
}