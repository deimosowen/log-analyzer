using LogAnalyzer.Infrastructure.Migrations;
using LogAnalyzer.Infrastructure.ClickHouse.Migrations;

namespace LogAnalyzer.Infrastructure.ClickHouse.Migrations.Events;

internal sealed class V001_CreateLogEventSchema : ClickHouseEventMigration
{
    public override DatabaseMigration Migration { get; } = DatabaseMigration.Create(
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
}
