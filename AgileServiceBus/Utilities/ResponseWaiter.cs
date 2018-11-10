using System.Collections.Concurrent;

namespace PhotosiMessageLibrary.Utilities
{
    public class ResponseWaiter
    {
        private ConcurrentDictionary<string, BlockingCollection<string>> _blockingCollections;
        private int _timeout;

        public ResponseWaiter(int timeout)
        {
            _blockingCollections = new ConcurrentDictionary<string, BlockingCollection<string>>();
            _timeout = timeout;
        }

        public void Register(string correlationId)
        {
            while (_blockingCollections.TryAdd(correlationId, new BlockingCollection<string>()) == false);
        }

        public void Resolve(string correlationId, string response)
        {
            if (_blockingCollections.ContainsKey(correlationId) == true)
                _blockingCollections[correlationId].Add(response);
        }

        public string Wait(string correlationId)
        {
            string response = null;
            _blockingCollections[correlationId].TryTake(out response, _timeout);

            return (response);
        }

        public void Unregister(string correlationId)
        {
            BlockingCollection<string> value;
            while (_blockingCollections.TryRemove(correlationId, out value) == false);
        }
    }
}