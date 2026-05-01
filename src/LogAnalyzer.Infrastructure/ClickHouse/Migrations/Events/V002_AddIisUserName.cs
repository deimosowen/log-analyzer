using LogAnalyzer.Infrastructure.Migrations;
using LogAnalyzer.Infrastructure.ClickHouse.Migrations;

namespace LogAnalyzer.Infrastructure.ClickHouse.Migrations.Events;

internal sealed class V002_AddIisUserName : ClickHouseEventMigration
{
    public override DatabaseMigration Migration { get; } = DatabaseMigration.Create(
        2,
        "Add IIS user name",
        """
        ALTER TABLE {event_table}
        ADD COLUMN IF NOT EXISTS user_name String AFTER client_ip
        """);
}
