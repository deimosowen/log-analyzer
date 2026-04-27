using LogAnalyzer.Infrastructure.Migrations;
using LogAnalyzer.Infrastructure.ClickHouse.Migrations;

namespace LogAnalyzer.Infrastructure.ClickHouse;

public sealed class ClickHouseEventStoreMigrator : IDatabaseMigrator
{
    private static readonly IReadOnlyList<DatabaseMigration> MigrationSet =
        DatabaseMigrationCatalog.Load<ClickHouseEventMigration>();

    private readonly ClickHouseSqlClient _client;

    public ClickHouseEventStoreMigrator(ClickHouseSqlClient client)
    {
        _client = client;
    }

    public string StoreName => "clickhouse_events";

    private IReadOnlyList<DatabaseMigration> Migrations => MigrationSet;

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await _client.ExecuteAsync($"CREATE DATABASE IF NOT EXISTS {_client.Database}", cancellationToken);
        await EnsureMigrationTableAsync(cancellationToken);

        var appliedVersions = await ReadAppliedVersionsAsync(cancellationToken);
        foreach (var migration in Migrations.OrderBy(static migration => migration.Version))
        {
            if (appliedVersions.Contains(migration.Version))
            {
                continue;
            }

            await ApplyMigrationAsync(migration, cancellationToken);
        }
    }

    private async Task EnsureMigrationTableAsync(CancellationToken cancellationToken)
    {
        await _client.ExecuteAsync($$"""
            CREATE TABLE IF NOT EXISTS {{_client.MigrationTable}}
            (
                store String,
                version UInt32,
                name String,
                applied_at DateTime64(3, 'UTC')
            )
            ENGINE = MergeTree
            ORDER BY (store, version)
            """, cancellationToken);
    }

    private async Task<HashSet<int>> ReadAppliedVersionsAsync(CancellationToken cancellationToken)
    {
        var rows = await _client.QueryJsonEachRowAsync($$"""
            SELECT version
            FROM {{_client.MigrationTable}}
            WHERE store = {{ClickHouseSql.QuoteLiteral(StoreName)}}
            FORMAT JSONEachRow
            """, cancellationToken);

        return rows.Select(row => row.GetProperty("version").GetInt32()).ToHashSet();
    }

    private async Task ApplyMigrationAsync(
        DatabaseMigration migration,
        CancellationToken cancellationToken)
    {
        foreach (var statement in migration.Statements)
        {
            await _client.ExecuteAsync(Prepare(statement), cancellationToken);
        }

        await _client.ExecuteAsync($$"""
            INSERT INTO {{_client.MigrationTable}} (store, version, name, applied_at)
            VALUES (
                {{ClickHouseSql.QuoteLiteral(StoreName)}},
                {{migration.Version}},
                {{ClickHouseSql.QuoteLiteral(migration.Name)}},
                toDateTime64({{ClickHouseSql.QuoteLiteral(ClickHouseSql.ToDateTime64(DateTimeOffset.UtcNow))}}, 3, 'UTC')
            )
            """, cancellationToken);
    }

    private string Prepare(string statement)
    {
        return statement.Replace("{event_table}", _client.EventTable, StringComparison.Ordinal);
    }
}
