using System;
using System.Collections.Specialized;
using System.Runtime.Caching;

namespace LazyCache.UnitTests
{
    public class InMemoryCacheManager : IDisposable
    {
        private string cacheName;
        private NameValueCollection cacheConfigurationCollection;

        /// <summary>
        /// Create an in-memory cache using a specific instance of the MemoryCache.
        /// Memory usage can be configured in a standard config file, using the cache name LazyCache.
        /// -----------------------------------------------------------------------------------------------------------------------------
        /// cacheMemoryLimitMegabytes:   The amount of maximum memory size to be used. Specified in megabytes. 
        ///                              The default is zero, which indicates that the MemoryCache instance manages its own memory
        ///                              based on the amount of memory that is installed on the computer. 
        /// physicalMemoryPercentage:    The percentage of physical memory that the cache can use. It is specified as an integer value from 1 to 100. 
        ///                              The default is zero, which indicates that the MemoryCache instance manages its own memory 
        ///                              based on the amount of memory that is installed on the computer. 
        /// pollingInterval:             The time interval after which the cache implementation compares the current memory load with the 
        ///                              absolute and percentage-based memory limits that are set for the cache instance.
        ///                              The default is two minutes.
        /// -----------------------------------------------------------------------------------------------------------------------------
        ///  <configuration>
        ///    <system.runtime.caching>
        ///      <memoryCache>
        ///        <namedCaches>
        ///          <add name="LazyCache" cacheMemoryLimitMegabytes="0" physicalMemoryPercentage="0" pollingInterval="00:02:00" />
        ///        </namedCaches>
        ///      </memoryCache>
        ///    </system.runtime.caching>
        ///  </configuration>
        /// </summary>
        public InMemoryCacheManager(string name = "LazyCache", NameValueCollection collection = null)
        {
            cacheName = name;
            cacheConfigurationCollection = collection;
            cacheService = BuildCache();
        }

        private CachingService BuildCache()
        {
            var inMemoryCache = new MemoryCache(cacheName, cacheConfigurationCollection);
            return new CachingService(inMemoryCache);
        }

        private CachingService cacheService;
        public IAppCache Cache => cacheService;
        public void Dispose()
        {
            DisposeCache(cacheService);
            cacheService = null;
            GC.SuppressFinalize(this);
        }

        private static void DisposeCache(CachingService cacheService)
        {
            var disposable = cacheService?.ObjectCache as IDisposable;

            if (disposable != null)
            {
                disposable.Dispose();
            }

            if (cacheService?.ObjectCache != null)
            {
                cacheService.ObjectCache = null;
            }
        }

        public void ClearAll()
        {
            var oldCache = cacheService;
            var newCache = BuildCache();
            cacheService = newCache;
            DisposeCache(oldCache);
        }
    }
}