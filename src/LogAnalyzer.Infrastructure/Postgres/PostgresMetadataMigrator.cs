using LogAnalyzer.Infrastructure.Migrations;
using LogAnalyzer.Infrastructure.Postgres.Migrations;

namespace LogAnalyzer.Infrastructure.Postgres;

public sealed class PostgresMetadataMigrator : PostgresDatabaseMigrator
{
    private static readonly IReadOnlyList<DatabaseMigration> MigrationSet =
        DatabaseMigrationCatalog.Load<PostgresMetadataMigration>();

    public PostgresMetadataMigrator(PostgresConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    public override string StoreName => "postgres_metadata";

    protected override IReadOnlyList<DatabaseMigration> Migrations => MigrationSet;
}
