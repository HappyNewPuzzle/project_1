using System.Collections.Concurrent;

public sealed class GatewayRouter
{
    private readonly ConcurrentDictionary<string, WorldBackend> backends = new();
    private readonly TimeSpan heartbeatTimeout;
    private readonly Func<DateTimeOffset> getCurrentTime;
    public GatewayRouter(TimeSpan heartbeatTimeout, Func<DateTimeOffset>? getCurrentTime = null)
    { this.heartbeatTimeout = heartbeatTimeout; this.getCurrentTime = getCurrentTime ?? (() => DateTimeOffset.UtcNow); }
    public void Upsert(WorldBackend backend) => backends[backend.ServerId] = backend;
    public WorldBackend? Select(int mapId) => backends.Values
        .Where(item => item.MapId == mapId && item.CanAccept(getCurrentTime(), heartbeatTimeout))
        .OrderBy(item => (double)item.ActivePlayers / item.Capacity)
        .ThenBy(item => item.ServerId, StringComparer.Ordinal)
        .FirstOrDefault();
    public bool Remove(string serverId) => backends.TryRemove(serverId, out _);
}
