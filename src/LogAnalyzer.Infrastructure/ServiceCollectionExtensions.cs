using LogAnalyzer.Application;
using LogAnalyzer.Application.Parsing;
using LogAnalyzer.Infrastructure.ClickHouse;
using LogAnalyzer.Infrastructure.Migrations;
using LogAnalyzer.Infrastructure.Sqlite;
using LogAnalyzer.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LogAnalyzer.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLogAnalyzerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection("Storage"));
        services.Configure<SqliteOptions>(configuration.GetSection("Sqlite"));
        services.Configure<ClickHouseOptions>(configuration.GetSection("ClickHouse"));

        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<IDatabaseMigrator, SqliteMetadataMigrator>();
        services.AddSingleton<IMetadataRepository, SqliteMetadataRepository>();
        services.AddSingleton<ILogFileStorage, FileSystemLogStorage>();
        services.AddSingleton<IImportJobQueue, ImportJobQueue>();
        services.AddSingleton<ImportProcessor>();

        services.AddSingleton<ILogParser, PipeSeparatedAppLogParser>();
        services.AddSingleton<ILogParser, NLogPlainParser>();
        services.AddSingleton<ILogParser, IisW3CParser>();
        services.AddSingleton<ILogParser, FallbackTextParser>();
        services.AddSingleton<LogParserSelector>();

        var clickHouseEnabled = configuration.GetValue<bool>("ClickHouse:Enabled");
        if (clickHouseEnabled)
        {
            services.AddHttpClient<ClickHouseSqlClient>();
            services.AddSingleton<ClickHouseLogEventStore>();
            services.AddSingleton<IDatabaseMigrator, ClickHouseEventStoreMigrator>();
            services.AddSingleton<ILogEventStore>(provider => provider.GetRequiredService<ClickHouseLogEventStore>());
        }
        else
        {
            services.AddSingleton<IDatabaseMigrator, SqliteEventStoreMigrator>();
            services.AddSingleton<ILogEventStore, SqliteLogEventStore>();
        }

        return services;
    }
}
