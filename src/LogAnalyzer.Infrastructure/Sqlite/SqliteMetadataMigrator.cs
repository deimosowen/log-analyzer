using LogAnalyzer.Infrastructure.Migrations;
using LogAnalyzer.Infrastructure.Sqlite.Migrations;

namespace LogAnalyzer.Infrastructure.Sqlite;

public sealed class SqliteMetadataMigrator : SqliteDatabaseMigrator
{
    private static readonly IReadOnlyList<DatabaseMigration> MigrationSet =
        DatabaseMigrationCatalog.Load<SqliteMetadataMigration>();

    public SqliteMetadataMigrator(SqliteConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    public override string StoreName => "sqlite_metadata";

    protected override IReadOnlyList<DatabaseMigration> Migrations => MigrationSet;
}
