using LogAnalyzer.Infrastructure.ClickHouse.Migrations;
using LogAnalyzer.Infrastructure.Migrations;
using LogAnalyzer.Infrastructure.Postgres.Migrations;
using LogAnalyzer.Infrastructure.Sqlite.Migrations;

namespace LogAnalyzer.Tests;

public sealed class MigrationCatalogTests
{
    [Fact]
    public void DiscoversSqliteMetadataMigrationsInVersionOrder()
    {
        var migrations = DatabaseMigrationCatalog.Load<SqliteMetadataMigration>();

        Assert.Equal([1, 2], migrations.Select(static migration => migration.Version));
    }

    [Fact]
    public void DiscoversPostgresMetadataMigrationsInVersionOrder()
    {
        var migrations = DatabaseMigrationCatalog.Load<PostgresMetadataMigration>();

        Assert.Equal([1, 2], migrations.Select(static migration => migration.Version));
    }

    [Fact]
    public void DiscoversSqliteEventMigrations()
    {
        var migrations = DatabaseMigrationCatalog.Load<SqliteEventMigration>();

        Assert.Equal([1, 2], migrations.Select(static migration => migration.Version));
    }

    [Fact]
    public void DiscoversClickHouseEventMigrations()
    {
        var migrations = DatabaseMigrationCatalog.Load<ClickHouseEventMigration>();

        Assert.Equal([1, 2], migrations.Select(static migration => migration.Version));
        Assert.Contains("{event_table}", migrations[0].Statements[0], StringComparison.Ordinal);
    }
}
