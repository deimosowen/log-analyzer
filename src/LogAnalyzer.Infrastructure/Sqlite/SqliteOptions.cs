namespace LogAnalyzer.Infrastructure.Sqlite;

public sealed class SqliteOptions
{
    public string DatabasePath { get; set; } = "data/log-analyzer.db";
}
