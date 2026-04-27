namespace LogAnalyzer.Infrastructure.Migrations;

public interface IDatabaseMigrationDefinition
{
    DatabaseMigration Migration { get; }
}
