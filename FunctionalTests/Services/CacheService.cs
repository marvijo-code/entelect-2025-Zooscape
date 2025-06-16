using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace FunctionalTests.Services;

public class CacheService
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl)
    {
        if (_cache.TryGetValue(key, out var entry) && entry != null && !entry.IsExpired())
        {
            return (T)entry.Value;
        }

        var value = await factory();
        _cache[key] = new CacheEntry(value, ttl);
        return value;
    }

    public void Clear()
    {
        _cache.Clear();
    }

    private class CacheEntry
    {
        public object? Value { get; }
        private readonly DateTime _createdAt = DateTime.UtcNow;
        private readonly TimeSpan _ttl;

        public CacheEntry(object? value, TimeSpan ttl)
        {
            Value = value;
            _ttl = ttl;
        }

        public bool IsExpired() => DateTime.UtcNow - _createdAt > _ttl;
    }
}
