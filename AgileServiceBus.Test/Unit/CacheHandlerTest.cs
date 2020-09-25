using AgileServiceBus.Interfaces;
using AgileServiceBus.Utilities;
using System;
using Xunit;

namespace AgileServiceBus.Test.Unit
{
    public class CacheHandlerTest
    {
        [Fact]
        public void ExistentCachedRecovery()
        {
            using (CacheHandler cacheHandler = new CacheHandler(100, new TimeSpan(1, 0, 0)))
            {
                CacheId cacheId = new CacheId("prefix");
                cacheHandler.Set(cacheId, "cached");

                Assert.Equal("cached", cacheHandler.Get<string>(cacheId));
            }
        }

        [Fact]
        public void InexistentCachedRecovery()
        {
            using (CacheHandler cacheHandler = new CacheHandler(100, new TimeSpan(1, 0, 0)))
            {
                CacheId cacheId = new CacheId("prefix");

                Assert.Null(cacheHandler.Get<string>(cacheId));
            }
        }

        [Fact]
        public void CachedRewriting()
        {
            using (CacheHandler cacheHandler = new CacheHandler(100, new TimeSpan(1, 0, 0)))
            {
                CacheId cacheId = new CacheId("prefix");
                cacheHandler.Set(cacheId, "cached1");
                cacheHandler.Set(cacheId, "cached2");
                cacheHandler.Set(cacheId, "cached3");
                cacheHandler.Set(cacheId, "cached4");
                cacheHandler.Set(cacheId, "cached5");

                Assert.Equal("cached5", cacheHandler.Get<string>(cacheId));
            }
        }

        [Fact]
        public void OutOfLimitCachedRecovery()
        {
            using (CacheHandler cacheHandler = new CacheHandler(1, new TimeSpan(1, 0, 0)))
            {
                CacheId cacheId1 = new CacheId("prefix1");
                CacheId cacheId2 = new CacheId("prefix2");
                cacheHandler.Set(cacheId1, "cached1");
                cacheHandler.Set(cacheId2, "cached2");

                Assert.Equal("cached1", cacheHandler.Get<string>(cacheId1));
                Assert.Null(cacheHandler.Get<string>(cacheId2));
            }
        }

        private class CacheId : ICacheId
        {
            private string _prefix;

            public CacheId(string prefix)
            {
                _prefix = prefix;
            }

            public string CreateCacheSuffix()
            {
                return _prefix + "suffix";
            }
        }
    }
}