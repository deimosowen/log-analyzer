using LogAnalyzer.Infrastructure.Migrations;

namespace LogAnalyzer.Infrastructure.ClickHouse;

public sealed class ClickHouseEventStoreMigrator : IDatabaseMigrator
{
    private static readonly DatabaseMigration InitialSchema = DatabaseMigration.Create(
        1,
        "Create log event schema",
        """
        CREATE TABLE IF NOT EXISTS {event_table}
        (
            event_id String,
            project_id UUID,
            upload_session_id UUID,
            log_file_id UUID,

            timestamp_utc DateTime64(4, 'UTC'),
            timestamp_ms Int64,

            level LowCardinality(String),
            source String,
            thread_id String,

            line_number UInt64,
            end_line_number UInt64,
            byte_offset UInt64,

            message String,
            exception String,
            raw_text String,

            http_method LowCardinality(String),
            url String,
            status_code UInt16,
            client_ip String,
            server_ip String,
            user_agent String,
            time_taken UInt32
        )
        ENGINE = MergeTree
        PARTITION BY toYYYYMM(timestamp_utc)
        ORDER BY (project_id, timestamp_utc, log_file_id, line_number)
        """);

    private readonly ClickHouseSqlClient _client;

    public ClickHouseEventStoreMigrator(ClickHouseSqlClient client)
    {
        _client = client;
    }

    public string StoreName => "clickhouse_events";

    private IReadOnlyList<DatabaseMigration> Migrations => [InitialSchema];

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
