using System;
using NUnit.Framework;

namespace LazyCache.UnitTests
{
    [TestFixture()]
    public class InMemoryCacheManagerTests
    {
        private static readonly string CacheKey = "key";

        [Test]
        public void CreateAddAndThenDisposeTheCacheWithoutErrors()
        {
            using (var sut = new InMemoryCacheManager())
            {
                IAppCache cache = sut.Cache;
                var cachedItem = cache.GetOrAdd(CacheKey, () => new object());
                Assert.NotNull(cachedItem);
            }
        }

        [Test]
        public void ClearAllRemovesAnAddedCacheItem()
        {
            using (var sut = new InMemoryCacheManager())
            {
                sut.Cache.GetOrAdd(CacheKey, () => new object());
                sut.ClearAll();
                Assert.NotNull(sut.Cache);
                Assert.Null(sut.Cache.Get<object>(CacheKey));
            }
        }

        [Test]
        public void GetOrAddAfterClearAllAdds()
        {
            using (var sut = new InMemoryCacheManager())
            {
                sut.Cache.GetOrAdd(CacheKey, () => new DateTime(2016,11,29));
                sut.ClearAll();
                var after = sut.Cache.GetOrAdd(CacheKey, () => new DateTime(2016, 11, 30));
                Assert.That(after.Day, Is.EqualTo(30));
            }
        }

        [Test]
        public void CacheStillWorksAfterClearAll()
        {
            using (var sut = new InMemoryCacheManager())
            {
                sut.ClearAll();
                Assert.NotNull(sut.Cache.GetOrAdd(CacheKey, () => new object()));
            }
        }
    }
}