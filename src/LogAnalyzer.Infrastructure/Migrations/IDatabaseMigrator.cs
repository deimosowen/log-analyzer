namespace LogAnalyzer.Infrastructure.Migrations;

public interface IDatabaseMigrator
{
    string StoreName { get; }
    Task MigrateAsync(CancellationToken cancellationToken);
}
