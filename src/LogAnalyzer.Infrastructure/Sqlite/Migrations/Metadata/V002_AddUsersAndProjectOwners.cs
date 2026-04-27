using LogAnalyzer.Infrastructure.Migrations;

namespace LogAnalyzer.Infrastructure.Sqlite.Migrations.Metadata;

internal static class V002_AddUsersAndProjectOwners
{
    public static DatabaseMigration Migration { get; } = DatabaseMigration.Create(
        2,
        "Add users and project ownership",
        """
        CREATE TABLE IF NOT EXISTS app_users (
            id TEXT PRIMARY KEY,
            provider TEXT NOT NULL,
            provider_user_id TEXT NOT NULL,
            email TEXT NOT NULL,
            display_name TEXT NOT NULL,
            created_at TEXT NOT NULL,
            last_login_at TEXT NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ux_app_users_provider_subject ON app_users(provider, provider_user_id);
        CREATE INDEX IF NOT EXISTS ix_app_users_email ON app_users(email);
        """,
        """
        ALTER TABLE projects ADD COLUMN owner_user_id TEXT NOT NULL DEFAULT 'local-dev';
        """,
        """
        INSERT OR IGNORE INTO app_users
            (id, provider, provider_user_id, email, display_name, created_at, last_login_at)
        VALUES
            ('local-dev', 'development', 'local-dev', 'local@dev.local', 'Локальный пользователь', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'), strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
        """,
        """
        CREATE INDEX IF NOT EXISTS ix_projects_owner_updated ON projects(owner_user_id, updated_at);
        """);
}
