using AgileServiceBus.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AgileServiceBus.Utilities
{
    public class RetryHandler : IRetry
    {
        private ushort _minDelay;
        private ushort _maxDelay;
        private Random _random;
        private ushort _retryLimit;
        private List<Type> _includedExceptions;
        private List<Type> _excludedExceptions;
        private bool _inclusive;

        public RetryHandler(ushort minDelay, ushort maxDelay, ushort reryLimit, bool inclusive)
        {
            _minDelay = minDelay;
            _maxDelay = maxDelay;
            _random = new Random();
            _retryLimit = reryLimit;
            _includedExceptions = new List<Type>();
            _excludedExceptions = new List<Type>();
            _inclusive = inclusive;
        }

        public IIncludeForRetry IncludeForRetry<TException>() where TException : Exception
        {
            if (!_includedExceptions.Contains(typeof(TException)))
                _includedExceptions.Add(typeof(TException));

            if (_excludedExceptions.Contains(typeof(TException)))
                _excludedExceptions.Remove(typeof(TException));

            return this;
        }

        public IExcludeForRetry ExcludeForRetry<TException>() where TException : Exception
        {
            if (!_excludedExceptions.Contains(typeof(TException)))
                _excludedExceptions.Add(typeof(TException));

            if (_includedExceptions.Contains(typeof(TException)))
                _includedExceptions.Remove(typeof(TException));

            return this;
        }

        public bool IsForRetry(Exception exception)
        {
            if (_includedExceptions.Contains(exception.GetType()))
                return true;

            if (_excludedExceptions.Contains(exception.GetType()))
                return false;

            return !_inclusive;
        }

        public async Task ExecuteAsync(Func<Task> toRetry, Func<Exception, ushort, ushort, Task> error)
        {
            for (ushort i = 0; i <= _retryLimit; i++)
            {
                try
                {
                    await toRetry();

                    break;
                }
                catch (Exception exception)
                {
                    await error(exception, i, _retryLimit);

                    if (!IsForRetry(exception))
                        break;
                }

                await Task.Delay(_random.Next(_minDelay, _maxDelay));
            }
        }
    }
}