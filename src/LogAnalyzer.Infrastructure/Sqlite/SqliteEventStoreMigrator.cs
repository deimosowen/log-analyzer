using LogAnalyzer.Infrastructure.Migrations;
using LogAnalyzer.Infrastructure.Sqlite.Migrations;

namespace LogAnalyzer.Infrastructure.Sqlite;

public sealed class SqliteEventStoreMigrator : SqliteDatabaseMigrator
{
    private static readonly IReadOnlyList<DatabaseMigration> MigrationSet =
        DatabaseMigrationCatalog.Load<SqliteEventMigration>();

    public SqliteEventStoreMigrator(SqliteConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    public override string StoreName => "sqlite_events";

    protected override IReadOnlyList<DatabaseMigration> Migrations => MigrationSet;
}
