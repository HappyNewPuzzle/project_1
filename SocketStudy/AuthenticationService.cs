public sealed class AuthenticationService
{
    private readonly IAccountRepository accounts;
    private readonly PasswordHasher hasher;
    public AuthenticationService(IAccountRepository accounts, PasswordHasher hasher)
    { this.accounts = accounts; this.hasher = hasher; }
    public async Task<bool> RegisterAsync(long playerId, string password,
        CancellationToken cancellationToken = default) =>
        await accounts.CreateAsync(hasher.Hash(playerId, password), cancellationToken);
    public async Task<bool> VerifyAsync(long playerId, string password,
        CancellationToken cancellationToken = default) =>
        hasher.Verify(password, await accounts.FindAsync(playerId, cancellationToken));
}
