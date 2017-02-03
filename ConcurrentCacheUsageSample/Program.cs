// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// Forked from https://github.com/aspnet/Caching/blob/dev/samples/MemoryCacheConcurencySample/Program.cs


using System;
using System.Runtime.Caching;
using LazyCache;
using System.Threading.Tasks;

namespace ConcurrentCacheUsageSample
{
    public class Program
    {
        private const string Key = "LazyKey";
        private static readonly Random Random = new Random();
        private static CacheItemPolicy slidingCachePolicy;
        private static CacheItemPolicy absoluteCachePolicy;

        public static void Main()
        {
            slidingCachePolicy = new CacheItemPolicy()
            {
                SlidingExpiration = TimeSpan.FromSeconds(3),
                RemovedCallback = arguments => AfterEvicted(arguments.CacheItem.Key, arguments.CacheItem.Value, arguments.RemovedReason)
            };

            absoluteCachePolicy = new CacheItemPolicy()
            {
                AbsoluteExpiration = DateTimeOffset.Now.AddSeconds(7),
                RemovedCallback = arguments => AfterEvicted(arguments.CacheItem.Key, arguments.CacheItem.Value, arguments.RemovedReason)
            };

            var cache = new CachingService();

            SetKey(cache, GenerateRandomLengthValueToCache());

            PeriodicallyReadKey(cache, TimeSpan.FromSeconds(1));

            PeriodicallyRemoveKey(cache, TimeSpan.FromSeconds(11));

            PeriodicallySetKey(cache, TimeSpan.FromSeconds(13));

            Console.ReadLine();
            Console.WriteLine("Shutting down");
        }

        private static void SetKey(IAppCache cache, string value)
        {
            Console.WriteLine("Setting: " + value.Length + " length string");
            var policy = Random.Next(2) == 0 ? slidingCachePolicy : absoluteCachePolicy; // 50/50 on which policy
            cache.Add(Key, value, policy);
        }


        private static void AfterEvicted(object key, object value, CacheEntryRemovedReason reason)
        {
            Console.WriteLine("Evicted. Value: " + value.ToString().Length + " length string because " + reason);
        }

        private static void PeriodicallySetKey(IAppCache cache, TimeSpan interval)
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(interval);

                    var randomLengthValueToCache = GenerateRandomLengthValueToCache();

                    SetKey(cache, randomLengthValueToCache);
                }
            });
        }

        private static string GenerateRandomLengthValueToCache()
        {
            return new string('*', Random.Next(5000));
        }

        private static void PeriodicallyReadKey(IAppCache cache, TimeSpan interval)
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(interval);

                    if (Random.Next(3) == 0) // 1/3 chance
                    {
                        // Allow values to expire due to sliding refresh.
                        Console.WriteLine("Read skipped to allow occasional exiration at random choice.");
                    }
                    else
                    {
                        object result = cache.GetOrAdd(Key, GenerateRandomLengthValueToCache, slidingCachePolicy);
                        Console.WriteLine("Read " + (result != null ? (result.ToString().Length + " length"):  "null") + " string");
                    }
                }
            });
        }

        private static void PeriodicallyRemoveKey(IAppCache cache, TimeSpan interval)
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(interval);

                    Console.WriteLine("Removing...");
                    cache.Remove(Key);
                }
            });
        }
    }
}