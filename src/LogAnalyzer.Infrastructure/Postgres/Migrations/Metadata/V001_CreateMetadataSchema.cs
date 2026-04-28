using LogAnalyzer.Infrastructure.Migrations;
using LogAnalyzer.Infrastructure.Postgres.Migrations;

namespace LogAnalyzer.Infrastructure.Postgres.Migrations.Metadata;

internal sealed class V001_CreateMetadataSchema : PostgresMetadataMigration
{
    public override DatabaseMigration Migration { get; } = DatabaseMigration.Create(
        1,
        "Create metadata schema",
        """
        CREATE TABLE IF NOT EXISTS projects (
            id text PRIMARY KEY,
            name text NOT NULL,
            description text NULL,
            created_at timestamptz NOT NULL,
            updated_at timestamptz NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS upload_sessions (
            id text PRIMARY KEY,
            project_id text NOT NULL,
            status text NOT NULL,
            original_name text NULL,
            total_files integer NOT NULL DEFAULT 0,
            processed_files integer NOT NULL DEFAULT 0,
            total_lines bigint NOT NULL DEFAULT 0,
            processed_lines bigint NOT NULL DEFAULT 0,
            error_count integer NOT NULL DEFAULT 0,
            created_at timestamptz NOT NULL,
            finished_at timestamptz NULL,
            current_file text NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS log_files (
            id text PRIMARY KEY,
            project_id text NOT NULL,
            upload_session_id text NOT NULL,
            original_path text NOT NULL,
            stored_path text NOT NULL,
            display_name text NOT NULL,
            size_bytes bigint NOT NULL DEFAULT 0,
            hash text NULL,
            format text NOT NULL,
            timezone text NOT NULL,
            first_timestamp timestamptz NULL,
            last_timestamp timestamptz NULL,
            line_count bigint NOT NULL DEFAULT 0,
            status text NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS import_errors (
            id text PRIMARY KEY,
            upload_session_id text NOT NULL,
            log_file_id text NULL,
            line_number bigint NULL,
            error_message text NOT NULL,
            raw_text text NULL,
            created_at timestamptz NOT NULL
        );
        """,
        """
        CREATE INDEX IF NOT EXISTS ix_upload_sessions_project_id ON upload_sessions(project_id);
        """,
        """
        CREATE INDEX IF NOT EXISTS ix_log_files_project_id ON log_files(project_id);
        """,
        """
        CREATE INDEX IF NOT EXISTS ix_log_files_upload_session_id ON log_files(upload_session_id);
        """,
        """
        CREATE INDEX IF NOT EXISTS ix_import_errors_upload_session_id ON import_errors(upload_session_id);
        """);
}
