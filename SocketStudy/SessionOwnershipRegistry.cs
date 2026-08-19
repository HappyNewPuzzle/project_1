using System.Collections.Concurrent;

public sealed class SessionOwnershipRegistry : ISessionOwnershipStore
{
    private readonly ConcurrentDictionary<long, Guid> owners = new();
    public bool TryAcquire(long playerId, Guid connectionId) => owners.TryAdd(playerId, connectionId);
    public bool IsOwner(long playerId, Guid connectionId) =>
        owners.TryGetValue(playerId, out Guid owner) && owner == connectionId;
    public void Release(long playerId, Guid connectionId) =>
        owners.TryRemove(new KeyValuePair<long, Guid>(playerId, connectionId));
}
