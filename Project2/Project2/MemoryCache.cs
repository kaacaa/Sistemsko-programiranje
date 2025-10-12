using System;
using System.Collections.Concurrent;
using System.Threading;

public static class MemoryCache
{
    private class CacheItem
    {
        public string Value { get; set; }
        public DateTime AddedTime { get; set; }
        public Timer ExpirationTimer { get; set; }
    }

    private static ConcurrentDictionary<string, CacheItem> cache = new ConcurrentDictionary<string, CacheItem>();
    private static readonly TimeSpan defaultExpiration = TimeSpan.FromMinutes(30); // Podrazumevano vreme isteka: 30 minuta

    public static bool TryGet(string key, out string value)
    {
        value = null;
        if (cache.TryGetValue(key, out CacheItem item))
        {
            value = item.Value;
            return true;
        }
        return false;
    }

    public static void Add(string key, string value)
    {
        Add(key, value, defaultExpiration);
    }

    public static void Add(string key, string value, TimeSpan expiration)
    {
        var item = new CacheItem
        {
            Value = value,
            AddedTime = DateTime.Now
        };

        // Postavi timer za brisanje
        item.ExpirationTimer = new Timer(_ => RemoveExpiredItem(key), null, expiration, Timeout.InfiniteTimeSpan);

        cache[key] = item;
    }

    public static void Remove(string key)
    {
        if (cache.TryRemove(key, out CacheItem removedItem))
        {
            removedItem.ExpirationTimer?.Dispose();
        }
    }

    public static void Clear()
    {
        foreach (var key in cache.Keys)
        {
            Remove(key);
        }
    }

    private static void RemoveExpiredItem(string key)
    {
        if (cache.TryRemove(key, out CacheItem expiredItem))
        {
            expiredItem.ExpirationTimer?.Dispose();
            Logger.Log($"Kes istekao i uklonjen: {key} (dodat: {expiredItem.AddedTime})\n");
        }
    }

    // Metoda za dobijanje informacija o kesu (za debug)
    public static void PrintCacheInfo()
    {
        Logger.Log($"Trenutno stavki u kesu: {cache.Count}");
        foreach (var kvp in cache)
        {
            Logger.Log($"Key: {kvp.Key}, Added: {kvp.Value.AddedTime}");
        }
    }
}