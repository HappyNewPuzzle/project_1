using System.Text.Json;
using Microsoft.Data.Sqlite;

public sealed class SqliteCharacterRepository : ICharacterRepository
{
    private readonly string connectionString;
    private readonly JsonSerializerOptions jsonOptions = new();

    public SqliteCharacterRepository(string databasePath)
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

    public async Task<CharacterSaveData> SaveAsync(
        CharacterSaveData character,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        long nextVersion = checked(character.Version + 1);
        CharacterSaveData saved = character with { Version = nextVersion };
        string payload = JsonSerializer.Serialize(saved, jsonOptions);

        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        if (character.Version == 0)
        {
            command.CommandText =
                """
                INSERT INTO characters (player_id, version, payload, updated_at)
                VALUES ($playerId, $version, $payload, $updatedAt)
                ON CONFLICT(player_id) DO NOTHING;
                """;
        }
        else
        {
            command.CommandText =
                """
                UPDATE characters
                SET version = $version, payload = $payload, updated_at = $updatedAt
                WHERE player_id = $playerId AND version = $expectedVersion;
                """;
            command.Parameters.AddWithValue("$expectedVersion", character.Version);
        }

        command.Parameters.AddWithValue("$playerId", character.PlayerId);
        command.Parameters.AddWithValue("$version", nextVersion);
        command.Parameters.AddWithValue("$payload", payload);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));

        int affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affectedRows != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new CharacterConcurrencyException(character.PlayerId);
        }

        await transaction.CommitAsync(cancellationToken);
        return saved;
    }

    public async Task<CharacterSaveData?> LoadAsync(
        long playerId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM characters WHERE player_id = $playerId;";
        command.Parameters.AddWithValue("$playerId", playerId);
        object? payload = await command.ExecuteScalarAsync(cancellationToken);
        return payload is string json
            ? JsonSerializer.Deserialize<CharacterSaveData>(json, jsonOptions)
            : null;
    }

    private void Initialize()
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS schema_info (
                version INTEGER NOT NULL
            );
            INSERT INTO schema_info (version)
            SELECT 1 WHERE NOT EXISTS (SELECT 1 FROM schema_info);

            CREATE TABLE IF NOT EXISTS characters (
                player_id INTEGER PRIMARY KEY,
                version INTEGER NOT NULL,
                payload TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }
}
