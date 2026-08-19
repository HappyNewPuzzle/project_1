public static class DatabaseMigrationCatalog
{
    public static IReadOnlyList<DatabaseMigration> All { get; } =
    [
        new(1,
            "CREATE TABLE IF NOT EXISTS schema_migrations (version INTEGER PRIMARY KEY, applied_at TEXT NOT NULL);",
            "CREATE TABLE IF NOT EXISTS schema_migrations (version INTEGER PRIMARY KEY, applied_at TIMESTAMPTZ NOT NULL);"),
        new(2,
            "CREATE INDEX IF NOT EXISTS ix_characters_updated_at ON characters(updated_at);",
            "CREATE INDEX IF NOT EXISTS ix_characters_updated_at ON characters(updated_at);")
    ];
}
