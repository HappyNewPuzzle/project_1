public interface IAccountRepository
{
    Task<bool> CreateAsync(
        AccountCredential account,
        CancellationToken cancellationToken = default);

    Task<AccountCredential?> FindAsync(
        long playerId,
        CancellationToken cancellationToken = default);
}
