using LogAnalyzer.Infrastructure.Migrations;
using LogAnalyzer.Infrastructure.Postgres.Migrations;

namespace LogAnalyzer.Infrastructure.Postgres.Migrations.Metadata;

internal sealed class V003_ProjectMembersAndShareInvites : PostgresMetadataMigration
{
    public override DatabaseMigration Migration { get; } = DatabaseMigration.Create(
        3,
        "Project members and share invites",
        """
        CREATE TABLE IF NOT EXISTS project_members (
            project_id text NOT NULL REFERENCES projects (id) ON DELETE CASCADE,
            user_id text NOT NULL REFERENCES app_users (id) ON DELETE CASCADE,
            role text NOT NULL,
            created_at timestamptz NOT NULL,
            PRIMARY KEY (project_id, user_id)
        );
        """,
        """
        CREATE INDEX IF NOT EXISTS ix_project_members_user ON project_members (user_id);
        """,
        """
        CREATE TABLE IF NOT EXISTS project_share_invites (
            id text PRIMARY KEY,
            project_id text NOT NULL REFERENCES projects (id) ON DELETE CASCADE,
            token text NOT NULL,
            created_by_user_id text NOT NULL REFERENCES app_users (id),
            created_at timestamptz NOT NULL
        );
        """,
        """
        CREATE UNIQUE INDEX IF NOT EXISTS ux_project_share_invites_token ON project_share_invites (token);
        """,
        """
        CREATE INDEX IF NOT EXISTS ix_project_share_invites_project ON project_share_invites (project_id);
        """,
        """
        INSERT INTO project_members (project_id, user_id, role, created_at)
        SELECT id, owner_user_id, 'owner', updated_at FROM projects
        ON CONFLICT DO NOTHING;
        """);
}
