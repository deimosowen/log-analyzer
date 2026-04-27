using LogAnalyzer.Infrastructure.ClickHouse.Migrations;
using LogAnalyzer.Infrastructure.Migrations;
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
    public void DiscoversSqliteEventMigrations()
    {
        var migrations = DatabaseMigrationCatalog.Load<SqliteEventMigration>();

        var migration = Assert.Single(migrations);
        Assert.Equal(1, migration.Version);
        Assert.Equal("Create log event schema", migration.Name);
    }

    [Fact]
    public void DiscoversClickHouseEventMigrations()
    {
        var migrations = DatabaseMigrationCatalog.Load<ClickHouseEventMigration>();

        var migration = Assert.Single(migrations);
        Assert.Equal(1, migration.Version);
        Assert.Contains("{event_table}", migration.Statements[0], StringComparison.Ordinal);
    }
}
