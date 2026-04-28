using LogAnalyzer.Infrastructure.Migrations;
using Npgsql;

namespace LogAnalyzer.Infrastructure.Postgres;

public abstract class PostgresDatabaseMigrator : IDatabaseMigrator
{
    private readonly PostgresConnectionFactory _connectionFactory;

    protected PostgresDatabaseMigrator(PostgresConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public abstract string StoreName { get; }

    protected abstract IReadOnlyList<DatabaseMigration> Migrations { get; }

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await EnsureMigrationTableAsync(connection, cancellationToken);

        var appliedVersions = await ReadAppliedVersionsAsync(connection, cancellationToken);
        foreach (var migration in Migrations.OrderBy(static migration => migration.Version))
        {
            if (appliedVersions.Contains(migration.Version))
            {
                continue;
            }

            await ApplyMigrationAsync(connection, migration, cancellationToken);
        }
    }

    private static async Task EnsureMigrationTableAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                store text NOT NULL,
                version integer NOT NULL,
                name text NOT NULL,
                applied_at timestamptz NOT NULL,
                PRIMARY KEY (store, version)
            );
            """, cancellationToken);
    }

    private async Task<HashSet<int>> ReadAppliedVersionsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT version FROM schema_migrations WHERE store = @store;";
        command.Parameters.AddWithValue("@store", StoreName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var versions = new HashSet<int>();
        while (await reader.ReadAsync(cancellationToken))
        {
            versions.Add(reader.GetInt32(0));
        }

        return versions;
    }

    private async Task ApplyMigrationAsync(
        NpgsqlConnection connection,
        DatabaseMigration migration,
        CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var statement in migration.Statements)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (NpgsqlTransaction)transaction;
            command.CommandText = statement;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = (NpgsqlTransaction)transaction;
            command.CommandText = """
                INSERT INTO schema_migrations (store, version, name, applied_at)
                VALUES (@store, @version, @name, @applied_at);
                """;
            command.Parameters.AddWithValue("@store", StoreName);
            command.Parameters.AddWithValue("@version", migration.Version);
            command.Parameters.AddWithValue("@name", migration.Name);
            command.Parameters.AddWithValue("@applied_at", DateTimeOffset.UtcNow);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
