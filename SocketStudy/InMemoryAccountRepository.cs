public sealed class InMemoryAccountRepository : IAccountRepository
{
    private readonly object gate = new();
    private readonly Dictionary<long, AccountCredential> accounts = new();

    public Task<bool> CreateAsync(
        AccountCredential account,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult(accounts.TryAdd(account.PlayerId, account));
        }
    }

    public Task<AccountCredential?> FindAsync(
        long playerId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult(accounts.GetValueOrDefault(playerId));
        }
    }
}
