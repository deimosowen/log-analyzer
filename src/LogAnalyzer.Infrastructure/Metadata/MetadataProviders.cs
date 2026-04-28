namespace LogAnalyzer.Infrastructure.Metadata;

public static class MetadataProviders
{
    public const string Sqlite = "SQLite";
    public const string PostgreSql = "PostgreSQL";

    public static bool IsSqlite(string? provider)
    {
        return string.Equals(provider, Sqlite, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPostgreSql(string? provider)
    {
        return string.Equals(provider, PostgreSql, StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "PgSql", StringComparison.OrdinalIgnoreCase);
    }
}
