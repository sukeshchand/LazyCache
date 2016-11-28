using System;
using System.Runtime.Caching;
using System.Threading.Tasks;

namespace LazyCache
{
    public class CachingService : IAppCache
    {
        /// <summary>
        /// Create an in-memory cache using the default instance of the MemoryCache. 
        /// The default instance of MemoryCache is shared by default with all other instances of CachingService
        /// Memory usage can be configured in a standard config file.
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
        ///          <add name="default" cacheMemoryLimitMegabytes="0" physicalMemoryPercentage="0" pollingInterval="00:02:00" />
        ///        </namedCaches>
        ///      </memoryCache>
        ///    </system.runtime.caching>
        ///  </configuration>
        /// </summary>
        public CachingService() : this(MemoryCache.Default)
        {
        }

        /// <summary>
        /// Create a cache using a specified object cache. 
        /// CacheingService expects the consumer to manange the lifetime of the ObjectCache and does not dispose it
        /// </summary>
        /// <param name="cache">A cache implmentation such as MemoryCache</param>
        public CachingService(ObjectCache cache)
        {
            if (cache == null)
                throw new ArgumentNullException(nameof(cache));

            ObjectCache = cache;
            DefaultCacheDuration = 60*20;
        }

        /// <summary>
        /// Seconds to cache objects for by default
        /// </summary>
        public int DefaultCacheDuration { get; set; }

        private DateTimeOffset DefaultExpiryDateTime => DateTimeOffset.Now.AddSeconds(DefaultCacheDuration);

        public void Add<T>(string key, T item)
        {
            Add(key, item, DefaultExpiryDateTime);
        }

        public void Add<T>(string key, T item, DateTimeOffset expires)
        {
            Add(key, item, new CacheItemPolicy {AbsoluteExpiration = expires});
        }

        public void Add<T>(string key, T item, TimeSpan slidingExpiration)
        {
            Add(key, item, new CacheItemPolicy {SlidingExpiration = slidingExpiration});
        }

        public void Add<T>(string key, T item, CacheItemPolicy policy)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            ValidateKey(key);

            ObjectCache.Set(key, item, policy);
        }

        public T Get<T>(string key)
        {
            ValidateKey(key);

            var item = ObjectCache[key];

            return UnwrapLazy<T>(item);
        }


        public async Task<T> GetAsync<T>(string key)
        {
            ValidateKey(key);

            var item = ObjectCache[key];

            return await UnwrapAsyncLazys<T>(item);
        }


        public T GetOrAdd<T>(string key, Func<T> addItemFactory)
        {
            return GetOrAdd(key, addItemFactory, DefaultExpiryDateTime);
        }


        public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> addItemFactory, CacheItemPolicy policy)
        {
            ValidateKey(key);

            var newLazyCacheItem = new AsyncLazy<T>(addItemFactory);

            EnsureRemovedCallbackDoesNotReturnTheAsyncLazy<T>(policy);

            var existingCacheItem = ObjectCache.AddOrGetExisting(key, newLazyCacheItem, policy);

            if (existingCacheItem != null)
                return await UnwrapAsyncLazys<T>(existingCacheItem);

            try
            {
                var result = newLazyCacheItem.Value;

                if (result.IsCanceled || result.IsFaulted)
                    ObjectCache.Remove(key);

                return await result;
            }
            catch //addItemFactory errored so do not cache the exception
            {
                ObjectCache.Remove(key);
                throw;
            }
        }

        public T GetOrAdd<T>(string key, Func<T> addItemFactory, DateTimeOffset expires)
        {
            return GetOrAdd(key, addItemFactory, new CacheItemPolicy {AbsoluteExpiration = expires});
        }


        public T GetOrAdd<T>(string key, Func<T> addItemFactory, TimeSpan slidingExpiration)
        {
            return GetOrAdd(key, addItemFactory, new CacheItemPolicy {SlidingExpiration = slidingExpiration});
        }

        public T GetOrAdd<T>(string key, Func<T> addItemFactory, CacheItemPolicy policy)
        {
            ValidateKey(key);

            var newLazyCacheItem = new Lazy<T>(addItemFactory);

            EnsureRemovedCallbackDoesNotReturnTheLazy<T>(policy);

            var existingCacheItem = ObjectCache.AddOrGetExisting(key, newLazyCacheItem, policy);

            if (existingCacheItem != null)
                return UnwrapLazy<T>(existingCacheItem);

            try
            {
                return newLazyCacheItem.Value;
            }
            catch //addItemFactory errored so do not cache the exception
            {
                ObjectCache.Remove(key);
                throw;
            }
        }


        public void Remove(string key)
        {
            ValidateKey(key);
            ObjectCache.Remove(key);
        }

        public ObjectCache ObjectCache { get; internal set; }

        public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> addItemFactory)
        {
            return await GetOrAddAsync(key, addItemFactory, DefaultExpiryDateTime);
        }

        public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> addItemFactory, DateTimeOffset expires)
        {
            return await GetOrAddAsync(key, addItemFactory, new CacheItemPolicy {AbsoluteExpiration = expires});
        }

        public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> addItemFactory, TimeSpan slidingExpiration)
        {
            return await GetOrAddAsync(key, addItemFactory, new CacheItemPolicy {SlidingExpiration = slidingExpiration});
        }

        private static T UnwrapLazy<T>(object item)
        {
            var lazy = item as Lazy<T>;
            if (lazy != null)
                return lazy.Value;

            if (item is T)
                return (T) item;

            var asyncLazy = item as AsyncLazy<T>;
            if (asyncLazy != null)
                return asyncLazy.Value.Result;

            var task = item as Task<T>;
            if (task != null)
                return task.Result;

            return default(T);
        }

        private static async Task<T> UnwrapAsyncLazys<T>(object item)
        {
            var asyncLazy = item as AsyncLazy<T>;
            if (asyncLazy != null)
                return await asyncLazy.Value;

            var task = item as Task<T>;
            if (task != null)
                return await task;

            var lazy = item as Lazy<T>;
            if (lazy != null)
                return lazy.Value;

            if (item is T)
                return (T) item;

            return default(T);
        }

        private static void EnsureRemovedCallbackDoesNotReturnTheLazy<T>(CacheItemPolicy policy)
        {
            if (policy?.RemovedCallback != null)
            {
                var originallCallback = policy.RemovedCallback;
                policy.RemovedCallback = args =>
                {
                    //unwrap the cache item in a callback given one is specified
                    var item = args?.CacheItem?.Value as Lazy<T>;
                    if (item != null)
                        args.CacheItem.Value = item.IsValueCreated ? item.Value : default(T);
                    originallCallback(args);
                };
            }
        }

        private static void EnsureRemovedCallbackDoesNotReturnTheAsyncLazy<T>(CacheItemPolicy policy)
        {
            if (policy?.RemovedCallback != null)
            {
                var originallCallback = policy.RemovedCallback;
                policy.RemovedCallback = args =>
                {
                    //unwrap the cache item in a callback given one is specified
                    var item = args?.CacheItem?.Value as AsyncLazy<T>;
                    if (item != null)
                        args.CacheItem.Value = item.IsValueCreated ? item.Value : Task.FromResult(default(T));
                    originallCallback(args);
                };
            }
        }

        private void ValidateKey(string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentOutOfRangeException(nameof(key), "Cache keys cannot be empty or whitespace");
        }
    }
}