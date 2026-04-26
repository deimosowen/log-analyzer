namespace LogAnalyzer.Infrastructure.Migrations;

public sealed record DatabaseMigration(
    int Version,
    string Name,
    IReadOnlyList<string> Statements)
{
    public static DatabaseMigration Create(int version, string name, params string[] statements)
    {
        return new DatabaseMigration(version, name, statements);
    }
}
