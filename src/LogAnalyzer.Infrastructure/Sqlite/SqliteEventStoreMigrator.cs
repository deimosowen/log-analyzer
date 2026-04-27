using LogAnalyzer.Infrastructure.Migrations;
using LogAnalyzer.Infrastructure.Sqlite.Migrations.Events;

namespace LogAnalyzer.Infrastructure.Sqlite;

public sealed class SqliteEventStoreMigrator : SqliteDatabaseMigrator
{
    public SqliteEventStoreMigrator(SqliteConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    public override string StoreName => "sqlite_events";

    protected override IReadOnlyList<DatabaseMigration> Migrations =>
    [
        V001_CreateLogEventSchema.Migration
    ];
}
