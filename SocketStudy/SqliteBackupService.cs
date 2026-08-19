using Microsoft.Data.Sqlite;

public sealed class SqliteBackupService
{
    public void CreateBackup(string sourcePath, string destinationPath)
    {
        string source = Path.GetFullPath(sourcePath);
        string destination = Path.GetFullPath(destinationPath);
        if (!File.Exists(source)) throw new FileNotFoundException("Source database was not found.", source);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var sourceConnection = new SqliteConnection($"Data Source={source};Mode=ReadOnly;Pooling=False");
        using var destinationConnection = new SqliteConnection($"Data Source={destination};Pooling=False");
        sourceConnection.Open();
        destinationConnection.Open();
        sourceConnection.BackupDatabase(destinationConnection);
        if (!Verify(destination)) throw new InvalidDataException("Backup integrity check failed.");
    }

    public bool Verify(string databasePath)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={Path.GetFullPath(databasePath)};Mode=ReadOnly;Pooling=False");
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            return string.Equals(command.ExecuteScalar()?.ToString(), "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch (SqliteException) { return false; }
    }
}
