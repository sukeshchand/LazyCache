using System;
using System.Runtime.Caching;
using System.Threading.Tasks;

namespace LazyCache
{
    internal class CacheRemovalLazyUnwrapper<T>
    {
        private CacheEntryRemovedCallback originalCallback;

        internal CacheEntryRemovedCallback UnwrapLazyPolicyRemovedCallback(CacheEntryRemovedCallback originallCallback)
        {

            this.originalCallback = originallCallback;
            return RemovedCallback;
        }

        private void RemovedCallback(CacheEntryRemovedArguments args)
        {
            //unwrap the cache item in a callback given one is specified
            var item = args?.CacheItem?.Value as Lazy<T>;
            if (item != null)
                args.CacheItem.Value = item.IsValueCreated ? item.Value : default(T);
            originalCallback(args);
            originalCallback = null;
        }

        private void AsyncRemovedCallback(CacheEntryRemovedArguments args)
        {
            //unwrap the cache item in a callback given one is specified
            var item = args?.CacheItem?.Value as AsyncLazy<T>;
            if (item != null)
                args.CacheItem.Value = item.IsValueCreated ? item.Value : Task.FromResult(default(T));
            originalCallback(args);
            originalCallback = null;
        }

        internal CacheEntryRemovedCallback UnwrapAsyncLazyPolicyRemovedCallback(CacheEntryRemovedCallback originallCallback)
        {
            this.originalCallback = originallCallback;
            return AsyncRemovedCallback;
        }
    }
}