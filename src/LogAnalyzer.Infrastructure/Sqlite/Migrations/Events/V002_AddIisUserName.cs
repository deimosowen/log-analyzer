using LogAnalyzer.Infrastructure.Migrations;
using LogAnalyzer.Infrastructure.Sqlite.Migrations;

namespace LogAnalyzer.Infrastructure.Sqlite.Migrations.Events;

internal sealed class V002_AddIisUserName : SqliteEventMigration
{
    public override DatabaseMigration Migration { get; } = DatabaseMigration.Create(
        2,
        "Add IIS user name",
        """
        ALTER TABLE log_events ADD COLUMN user_name TEXT NOT NULL DEFAULT '';
        """);
}
