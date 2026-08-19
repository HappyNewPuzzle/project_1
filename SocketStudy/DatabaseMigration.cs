public sealed record DatabaseMigration(int Version, string SqliteSql, string PostgreSqlSql);
