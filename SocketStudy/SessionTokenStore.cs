using System.Security.Cryptography;
using System.Text;

public sealed class SessionTokenStore
{
    private sealed record StoredToken(long PlayerId, DateTimeOffset ExpiresAt);

    private readonly object gate = new();
    private readonly TimeSpan lifetime;
    private readonly Func<DateTimeOffset> getCurrentTime;
    private readonly Dictionary<string, StoredToken> tokensByHash =
        new(StringComparer.Ordinal);
    private readonly Dictionary<long, string> tokenHashByPlayer = new();

    public SessionTokenStore(
        TimeSpan lifetime,
        Func<DateTimeOffset>? getCurrentTime = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);
        this.lifetime = lifetime;
        this.getCurrentTime = getCurrentTime ?? (() => DateTimeOffset.UtcNow);
    }

    public string Issue(long playerId)
    {
        if (playerId <= PlayerSession.AnonymousPlayerId)
        {
            throw new ArgumentOutOfRangeException(nameof(playerId));
        }

        string token = SessionTokenGenerator.Create();
        string tokenHash = HashToken(token);
        DateTimeOffset now = getCurrentTime();
        lock (gate)
        {
            CleanupExpired(now);
            if (tokenHashByPlayer.Remove(playerId, out string? previousHash))
            {
                tokensByHash.Remove(previousHash);
            }

            tokensByHash[tokenHash] = new StoredToken(playerId, now + lifetime);
            tokenHashByPlayer[playerId] = tokenHash;
        }

        return token;
    }

    public SessionTokenValidation Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return SessionTokenValidation.Invalid;
        }

        string tokenHash = HashToken(token);
        DateTimeOffset now = getCurrentTime();
        lock (gate)
        {
            if (!tokensByHash.TryGetValue(tokenHash, out StoredToken? stored))
            {
                return SessionTokenValidation.Invalid;
            }

            if (stored.ExpiresAt <= now)
            {
                Remove(tokenHash, stored.PlayerId);
                return SessionTokenValidation.Invalid;
            }

            return new(true, stored.PlayerId, stored.ExpiresAt);
        }
    }

    public bool Revoke(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        string tokenHash = HashToken(token);
        lock (gate)
        {
            if (!tokensByHash.TryGetValue(tokenHash, out StoredToken? stored))
            {
                return false;
            }

            Remove(tokenHash, stored.PlayerId);
            return true;
        }
    }

    private void CleanupExpired(DateTimeOffset now)
    {
        (string Hash, long PlayerId)[] expired = tokensByHash
            .Where(pair => pair.Value.ExpiresAt <= now)
            .Select(pair => (pair.Key, pair.Value.PlayerId))
            .ToArray();
        foreach ((string hash, long playerId) in expired)
        {
            Remove(hash, playerId);
        }
    }

    private void Remove(string tokenHash, long playerId)
    {
        tokensByHash.Remove(tokenHash);
        if (tokenHashByPlayer.TryGetValue(playerId, out string? currentHash) &&
            currentHash == tokenHash)
        {
            tokenHashByPlayer.Remove(playerId);
        }
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
