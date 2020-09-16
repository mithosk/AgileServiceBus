using Microsoft.Extensions.Caching.Memory;
using System;

namespace AgileServiceBus.Utilities
{
    public class CacheHandler
    {
        private MemoryCache _memoryCache;
        private TimeSpan _duration;

        public CacheHandler(ushort limit, TimeSpan duration)
        {
            _memoryCache = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = limit
            });

            _duration = duration;
        }

        public void Set(string key, object toCache)
        {
            _memoryCache.Set(key, toCache, new MemoryCacheEntryOptions
            {
                Size = 1,
                AbsoluteExpirationRelativeToNow = _duration
            });
        }

        public TCached Get<TCached>(string key)
        {
            return _memoryCache.Get<TCached>(key);
        }
    }
}