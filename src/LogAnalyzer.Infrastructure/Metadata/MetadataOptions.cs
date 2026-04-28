namespace LogAnalyzer.Infrastructure.Metadata;

public sealed class MetadataOptions
{
    public string Provider { get; set; } = MetadataProviders.Sqlite;
}
