public interface ISharedCache
{
    void Set(string key, string value, TimeSpan lifetime);
    bool TryGet(string key, out string? value);
    bool Remove(string key);
}
