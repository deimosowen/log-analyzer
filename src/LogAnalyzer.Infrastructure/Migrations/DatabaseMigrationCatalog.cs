using System.Reflection;

namespace LogAnalyzer.Infrastructure.Migrations;

public static class DatabaseMigrationCatalog
{
    public static IReadOnlyList<DatabaseMigration> Load<TDefinition>()
        where TDefinition : IDatabaseMigrationDefinition
    {
        var definitionType = typeof(TDefinition);
        var migrations = definitionType.Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                           definitionType.IsAssignableFrom(type))
            .Select(static type => CreateMigrationDefinition<TDefinition>(type))
            .Select(static definition => definition.Migration)
            .OrderBy(static migration => migration.Version)
            .ToArray();

        Validate(definitionType, migrations);
        return migrations;
    }

    private static TDefinition CreateMigrationDefinition<TDefinition>(Type type)
        where TDefinition : IDatabaseMigrationDefinition
    {
        try
        {
            return (TDefinition)Activator.CreateInstance(type, nonPublic: true)!;
        }
        catch (Exception ex) when (ex is MissingMethodException or MemberAccessException)
        {
            throw new InvalidOperationException(
                $"Migration type {type.FullName} must have a parameterless constructor.",
                ex);
        }
    }

    private static void Validate(Type definitionType, IReadOnlyCollection<DatabaseMigration> migrations)
    {
        if (migrations.Count == 0)
        {
            throw new InvalidOperationException($"No migrations were found for {definitionType.Name}.");
        }

        var duplicateVersion = migrations
            .GroupBy(static migration => migration.Version)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicateVersion is not null)
        {
            var names = string.Join(", ", duplicateVersion.Select(static migration => migration.Name));
            throw new InvalidOperationException(
                $"Duplicate migration version {duplicateVersion.Key} for {definitionType.Name}: {names}.");
        }

        var invalidVersion = migrations.FirstOrDefault(static migration => migration.Version <= 0);
        if (invalidVersion is not null)
        {
            throw new InvalidOperationException(
                $"Migration {invalidVersion.Name} for {definitionType.Name} must have a positive version.");
        }
    }
}
