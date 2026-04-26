namespace LogAnalyzer.Infrastructure;

public sealed class StorageOptions
{
    public string RootPath { get; set; } = "storage";
    public long MaxUploadBytes { get; set; } = 1024L * 1024L * 1024L;
    public long MaxExtractedBytes { get; set; } = 5L * 1024L * 1024L * 1024L;
    public int MaxArchiveFiles { get; set; } = 10000;
    public int MaxArchiveDepth { get; set; } = 32;
}

public sealed class SqliteOptions
{
    public string DatabasePath { get; set; } = "data/log-analyzer.db";
}

public sealed class ClickHouseOptions
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "http://localhost:8123/";
    public string Database { get; set; } = "default";
    public string Username { get; set; } = "default";
    public string Password { get; set; } = string.Empty;
}
