using LogAnalyzer.Infrastructure.Migrations;
using LogAnalyzer.Infrastructure.Sqlite.Migrations.Metadata;

namespace LogAnalyzer.Infrastructure.Sqlite;

public sealed class SqliteMetadataMigrator : SqliteDatabaseMigrator
{
    public SqliteMetadataMigrator(SqliteConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    public override string StoreName => "sqlite_metadata";

    protected override IReadOnlyList<DatabaseMigration> Migrations =>
    [
        V001_CreateMetadataSchema.Migration,
        V002_AddUsersAndProjectOwners.Migration
    ];
}
