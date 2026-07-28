using System.Security.Cryptography;

public sealed class PasswordHasher
{
    public const int DefaultIterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private readonly AccountCredential dummyAccount;

    public PasswordHasher()
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        dummyAccount = new AccountCredential(
            PlayerSession.AnonymousPlayerId,
            salt,
            RandomNumberGenerator.GetBytes(HashSize),
            DefaultIterations);
    }

    public AccountCredential Hash(long playerId, string password)
    {
        ValidatePassword(password);
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            DefaultIterations,
            HashAlgorithmName.SHA256,
            HashSize);
        return new AccountCredential(playerId, salt, hash, DefaultIterations);
    }

    public bool Verify(string password, AccountCredential? account)
    {
        AccountCredential selectedAccount = account ?? dummyAccount;
        if (string.IsNullOrEmpty(password) ||
            selectedAccount.Iterations <= 0 ||
            selectedAccount.PasswordSalt.Length == 0 ||
            selectedAccount.PasswordHash.Length == 0)
        {
            return false;
        }

        byte[] candidate = Rfc2898DeriveBytes.Pbkdf2(
            password,
            selectedAccount.PasswordSalt,
            selectedAccount.Iterations,
            HashAlgorithmName.SHA256,
            selectedAccount.PasswordHash.Length);
        bool matches = CryptographicOperations.FixedTimeEquals(
            candidate,
            selectedAccount.PasswordHash);
        return account is not null && matches;
    }

    public static void ValidatePassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        if (password.Length is < 8 or > 128)
        {
            throw new ArgumentException("Password must be between 8 and 128 characters.", nameof(password));
        }
    }
}
