using LogAnalyzer.Infrastructure.Migrations;

namespace LogAnalyzer.Infrastructure.Sqlite.Migrations;

public abstract class SqliteMetadataMigration : IDatabaseMigrationDefinition
{
    public abstract DatabaseMigration Migration { get; }
}
