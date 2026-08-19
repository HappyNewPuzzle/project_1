public interface ISessionOwnershipStore
{
    bool TryAcquire(long playerId, Guid connectionId);
    bool IsOwner(long playerId, Guid connectionId);
    void Release(long playerId, Guid connectionId);
}
