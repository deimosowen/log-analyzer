using LogAnalyzer.Infrastructure.Migrations;
using LogAnalyzer.Infrastructure.Sqlite.Migrations;

namespace LogAnalyzer.Infrastructure.Sqlite.Migrations.Metadata;

internal sealed class V003_ProjectMembersAndShareInvites : SqliteMetadataMigration
{
    public override DatabaseMigration Migration { get; } = DatabaseMigration.Create(
        3,
        "Project members and share invites",
        """
        CREATE TABLE IF NOT EXISTS project_members (
            project_id TEXT NOT NULL,
            user_id TEXT NOT NULL,
            role TEXT NOT NULL,
            created_at TEXT NOT NULL,
            PRIMARY KEY (project_id, user_id)
        );
        """,
        """
        CREATE INDEX IF NOT EXISTS ix_project_members_user ON project_members (user_id);
        """,
        """
        CREATE TABLE IF NOT EXISTS project_share_invites (
            id TEXT PRIMARY KEY,
            project_id TEXT NOT NULL,
            token TEXT NOT NULL UNIQUE,
            created_by_user_id TEXT NOT NULL,
            created_at TEXT NOT NULL
        );
        """,
        """
        CREATE INDEX IF NOT EXISTS ix_project_share_invites_project ON project_share_invites (project_id);
        """,
        """
        INSERT OR IGNORE INTO project_members (project_id, user_id, role, created_at)
        SELECT id, owner_user_id, 'owner', updated_at FROM projects;
        """);
}
