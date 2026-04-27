using LogAnalyzer.Infrastructure.Migrations;

namespace LogAnalyzer.Infrastructure.Sqlite.Migrations.Metadata;

internal static class V001_CreateMetadataSchema
{
    public static DatabaseMigration Migration { get; } = DatabaseMigration.Create(
        1,
        "Create metadata schema",
        """
        CREATE TABLE IF NOT EXISTS projects (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            description TEXT NULL,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS upload_sessions (
            id TEXT PRIMARY KEY,
            project_id TEXT NOT NULL,
            status TEXT NOT NULL,
            original_name TEXT NULL,
            total_files INTEGER DEFAULT 0,
            processed_files INTEGER DEFAULT 0,
            total_lines INTEGER DEFAULT 0,
            processed_lines INTEGER DEFAULT 0,
            error_count INTEGER DEFAULT 0,
            created_at TEXT NOT NULL,
            finished_at TEXT NULL,
            current_file TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS log_files (
            id TEXT PRIMARY KEY,
            project_id TEXT NOT NULL,
            upload_session_id TEXT NOT NULL,
            original_path TEXT NOT NULL,
            stored_path TEXT NOT NULL,
            display_name TEXT NOT NULL,
            size_bytes INTEGER,
            hash TEXT,
            format TEXT,
            timezone TEXT,
            first_timestamp TEXT NULL,
            last_timestamp TEXT NULL,
            line_count INTEGER DEFAULT 0,
            status TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS import_errors (
            id TEXT PRIMARY KEY,
            upload_session_id TEXT NOT NULL,
            log_file_id TEXT NULL,
            line_number INTEGER NULL,
            error_message TEXT NOT NULL,
            raw_text TEXT NULL,
            created_at TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_upload_sessions_project_id ON upload_sessions(project_id);
        CREATE INDEX IF NOT EXISTS ix_log_files_project_id ON log_files(project_id);
        CREATE INDEX IF NOT EXISTS ix_log_files_upload_session_id ON log_files(upload_session_id);
        CREATE INDEX IF NOT EXISTS ix_import_errors_upload_session_id ON import_errors(upload_session_id);
        """);
}
