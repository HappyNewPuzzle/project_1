using Npgsql;

public sealed class PostgreSqlMigrationRunner
{
    private readonly string connectionString;
    public PostgreSqlMigrationRunner(string connectionString) => this.connectionString = connectionString;
    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (DatabaseMigration migration in DatabaseMigrationCatalog.All)
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = migration.PostgreSqlSql +
                "\nINSERT INTO schema_migrations(version, applied_at) VALUES (@version, NOW()) ON CONFLICT(version) DO NOTHING;";
            command.Parameters.AddWithValue("version", migration.Version);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }
}
