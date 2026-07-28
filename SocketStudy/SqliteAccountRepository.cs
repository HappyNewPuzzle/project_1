using Microsoft.Data.Sqlite;

public sealed class SqliteAccountRepository : IAccountRepository
{
    private readonly string connectionString;

    public SqliteAccountRepository(string databasePath)
    {
        string fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();
        Initialize();
    }

    public async Task<bool> CreateAsync(
        AccountCredential account,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO accounts (player_id, password_salt, password_hash, iterations, created_at)
            VALUES ($playerId, $salt, $hash, $iterations, $createdAt)
            ON CONFLICT(player_id) DO NOTHING;
            """;
        command.Parameters.AddWithValue("$playerId", account.PlayerId);
        command.Parameters.AddWithValue("$salt", account.PasswordSalt);
        command.Parameters.AddWithValue("$hash", account.PasswordHash);
        command.Parameters.AddWithValue("$iterations", account.Iterations);
        command.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<AccountCredential?> FindAsync(
        long playerId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT password_salt, password_hash, iterations
            FROM accounts
            WHERE player_id = $playerId;
            """;
        command.Parameters.AddWithValue("$playerId", playerId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AccountCredential(
            playerId,
            (byte[])reader["password_salt"],
            (byte[])reader["password_hash"],
            reader.GetInt32(2));
    }

    private void Initialize()
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS accounts (
                player_id INTEGER PRIMARY KEY,
                password_salt BLOB NOT NULL,
                password_hash BLOB NOT NULL,
                iterations INTEGER NOT NULL,
                created_at TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }
}
