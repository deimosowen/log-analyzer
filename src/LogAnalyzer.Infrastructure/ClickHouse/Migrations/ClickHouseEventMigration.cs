using LogAnalyzer.Infrastructure.Migrations;

namespace LogAnalyzer.Infrastructure.ClickHouse.Migrations;

public abstract class ClickHouseEventMigration : IDatabaseMigrationDefinition
{
    public abstract DatabaseMigration Migration { get; }
}
