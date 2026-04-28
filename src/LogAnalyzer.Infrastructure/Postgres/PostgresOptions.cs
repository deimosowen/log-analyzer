namespace LogAnalyzer.Infrastructure.Postgres;

public sealed class PostgresOptions
{
    public string? ConnectionString { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "log_analyzer";
    public string Username { get; set; } = "log_analyzer";
    public string Password { get; set; } = string.Empty;
    public bool Pooling { get; set; } = true;
}
