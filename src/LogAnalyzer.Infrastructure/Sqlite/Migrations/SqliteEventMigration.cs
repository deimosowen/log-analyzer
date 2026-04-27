using LogAnalyzer.Infrastructure.Migrations;

namespace LogAnalyzer.Infrastructure.Sqlite.Migrations;

public abstract class SqliteEventMigration : IDatabaseMigrationDefinition
{
    public abstract DatabaseMigration Migration { get; }
}
