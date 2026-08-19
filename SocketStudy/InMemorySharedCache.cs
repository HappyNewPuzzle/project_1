using System.Collections.Concurrent;

public sealed class InMemorySharedCache : ISharedCache
{
    private sealed record Entry(string Value, DateTimeOffset ExpiresAt);
    private readonly ConcurrentDictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly Func<DateTimeOffset> getCurrentTime;
    public InMemorySharedCache(Func<DateTimeOffset>? getCurrentTime = null) =>
        this.getCurrentTime = getCurrentTime ?? (() => DateTimeOffset.UtcNow);
    public void Set(string key, string value, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);
        entries[key] = new Entry(value, getCurrentTime() + lifetime);
    }
    public bool TryGet(string key, out string? value)
    {
        value = null;
        if (!entries.TryGetValue(key, out Entry? entry)) return false;
        if (entry.ExpiresAt <= getCurrentTime()) { entries.TryRemove(key, out _); return false; }
        value = entry.Value;
        return true;
    }
    public bool Remove(string key) => entries.TryRemove(key, out _);
}
