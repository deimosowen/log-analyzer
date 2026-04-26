namespace LogAnalyzer.Infrastructure.ClickHouse;

public sealed class ClickHouseOptions
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "http://localhost:8123/";
    public string Database { get; set; } = "default";
    public string Username { get; set; } = "default";
    public string Password { get; set; } = string.Empty;
}
