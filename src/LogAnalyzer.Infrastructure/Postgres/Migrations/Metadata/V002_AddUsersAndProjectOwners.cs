using LogAnalyzer.Infrastructure.Migrations;
using LogAnalyzer.Infrastructure.Postgres.Migrations;

namespace LogAnalyzer.Infrastructure.Postgres.Migrations.Metadata;

internal sealed class V002_AddUsersAndProjectOwners : PostgresMetadataMigration
{
    public override DatabaseMigration Migration { get; } = DatabaseMigration.Create(
        2,
        "Add users and project ownership",
        """
        CREATE TABLE IF NOT EXISTS app_users (
            id text PRIMARY KEY,
            provider text NOT NULL,
            provider_user_id text NOT NULL,
            email text NOT NULL,
            display_name text NOT NULL,
            created_at timestamptz NOT NULL,
            last_login_at timestamptz NOT NULL
        );
        """,
        """
        CREATE UNIQUE INDEX IF NOT EXISTS ux_app_users_provider_subject ON app_users(provider, provider_user_id);
        """,
        """
        CREATE INDEX IF NOT EXISTS ix_app_users_email ON app_users(email);
        """,
        """
        ALTER TABLE projects ADD COLUMN IF NOT EXISTS owner_user_id text NOT NULL DEFAULT 'local-dev';
        """,
        """
        INSERT INTO app_users
            (id, provider, provider_user_id, email, display_name, created_at, last_login_at)
        VALUES
            ('local-dev', 'development', 'local-dev', 'local@dev.local', 'Локальный пользователь', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
        ON CONFLICT (id) DO NOTHING;
        """,
        """
        CREATE INDEX IF NOT EXISTS ix_projects_owner_updated ON projects(owner_user_id, updated_at);
        """);
}
