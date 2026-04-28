using LogAnalyzer.Infrastructure.Migrations;

namespace LogAnalyzer.Infrastructure.Postgres.Migrations;

public abstract class PostgresMetadataMigration : IDatabaseMigrationDefinition
{
    public abstract DatabaseMigration Migration { get; }
}
