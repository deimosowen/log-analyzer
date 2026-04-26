using LogAnalyzer.Infrastructure.Migrations;

namespace LogAnalyzer.Infrastructure.Sqlite;

public sealed class SqliteEventStoreMigrator : SqliteDatabaseMigrator
{
    private static readonly DatabaseMigration InitialSchema = DatabaseMigration.Create(
        1,
        "Create log event schema",
        """
        CREATE TABLE IF NOT EXISTS log_events (
            event_id TEXT PRIMARY KEY,
            project_id TEXT NOT NULL,
            upload_session_id TEXT NOT NULL,
            log_file_id TEXT NOT NULL,
            timestamp_utc TEXT NOT NULL,
            timestamp_ms INTEGER NOT NULL,
            level TEXT NOT NULL,
            source TEXT NOT NULL,
            thread_id TEXT NOT NULL,
            line_number INTEGER NOT NULL,
            end_line_number INTEGER NOT NULL,
            byte_offset INTEGER NOT NULL,
            message TEXT NOT NULL,
            exception TEXT NOT NULL,
            raw_text TEXT NOT NULL,
            http_method TEXT NOT NULL,
            url TEXT NOT NULL,
            status_code INTEGER NOT NULL,
            client_ip TEXT NOT NULL,
            server_ip TEXT NOT NULL,
            user_agent TEXT NOT NULL,
            time_taken INTEGER NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_log_events_project_time ON log_events(project_id, timestamp_ms, log_file_id, line_number);
        CREATE INDEX IF NOT EXISTS ix_log_events_log_file ON log_events(log_file_id, timestamp_ms);
        CREATE INDEX IF NOT EXISTS ix_log_events_project_level ON log_events(project_id, level, timestamp_ms);
        """);

    public SqliteEventStoreMigrator(SqliteConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    public override string StoreName => "sqlite_events";

    protected override IReadOnlyList<DatabaseMigration> Migrations => [InitialSchema];
}
