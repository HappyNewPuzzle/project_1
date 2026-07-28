public sealed record AccountCredential(
    long PlayerId,
    byte[] PasswordSalt,
    byte[] PasswordHash,
    int Iterations);
