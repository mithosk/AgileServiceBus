using AgileServiceBus.Interfaces;
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

        public void Set(ICacheId cacheId, object toCache)
        {
            _memoryCache.Set(CreateCacheKey(cacheId), toCache, new MemoryCacheEntryOptions
            {
                Size = 1,
                AbsoluteExpirationRelativeToNow = _duration
            });
        }

        public TCached Get<TCached>(ICacheId cacheId)
        {
            return _memoryCache.Get<TCached>(CreateCacheKey(cacheId));
        }

        private string CreateCacheKey(ICacheId cacheId)
        {
            return cacheId.GetType().FullName + "-" + cacheId.CreateCacheSuffix();
        }
    }
}