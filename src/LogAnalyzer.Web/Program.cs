using LogAnalyzer.Application;
using LogAnalyzer.Application.Dashboard;
using LogAnalyzer.Infrastructure;
using LogAnalyzer.Infrastructure.Migrations;
using LogAnalyzer.Infrastructure.Sqlite;
using LogAnalyzer.Infrastructure.Storage;
using LogAnalyzer.Web.Api;
using LogAnalyzer.Web.Auth;
using LogAnalyzer.Web.Components;
using LogAnalyzer.Web.Reporting;
using LogAnalyzer.Web.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
var maxUploadBytes = builder.Configuration.GetValue<long?>("Storage:MaxUploadBytes") ??
                     StorageDefaults.MaxUploadBytes;

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxUploadBytes;
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedHost |
        ForwardedHeaders.XForwardedProto;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxUploadBytes;
});
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<UserDashboardService>();
builder.Services.AddSingleton<IReportPdfRenderer, QuestPdfMarkdownReportRenderer>();
builder.Services.AddLogAnalyzerAuthentication(builder.Configuration);
builder.Services.AddLogAnalyzerInfrastructure(builder.Configuration);
builder.Services.AddSingleton<ImportCancellationRegistry>();
builder.Services.AddHostedService<ImportWorkerService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseLogAnalyzerAuthentication();
app.UseAntiforgery();
app.MapLogAnalyzerAuth();
app.MapLogAnalyzerApi();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await InitializeStorageAsync(app.Services);

app.Run();

static async Task InitializeStorageAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var provider = scope.ServiceProvider;
    var logger = provider.GetService<ILoggerFactory>()?.CreateLogger("StorageInit");

    var sqliteFactory = provider.GetService<SqliteConnectionFactory>();
    if (sqliteFactory is not null)
    {
        logger?.LogInformation("SQLite database file: {DatabasePath}", sqliteFactory.DatabasePath);
    }

    foreach (var migrator in provider.GetServices<IDatabaseMigrator>())
    {
        logger?.LogInformation("Applying database migrations: {Migrator}", migrator.GetType().Name);
        await migrator.MigrateAsync(CancellationToken.None);
    }

    await RepairSqliteMetadataProjectMembersIfNeededAsync(provider, logger);
}

/// <summary>
/// Если таблицы шаринга нет (например, после прерванного деплоя или рассинхрона schema_migrations),
/// сбрасываем отметку о версии 3 и повторно прогоняем только sqlite_metadata.
/// </summary>
static async Task RepairSqliteMetadataProjectMembersIfNeededAsync(IServiceProvider provider, ILogger? logger)
{
    if (provider.GetService<IMetadataRepository>() is not SqliteMetadataRepository)
    {
        return;
    }

    var factory = provider.GetRequiredService<SqliteConnectionFactory>();
    await using (var connection = await factory.OpenAsync(CancellationToken.None))
    {
        await using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = """
            SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'project_members' LIMIT 1;
            """;
        if (await checkCmd.ExecuteScalarAsync(CancellationToken.None) is not null)
        {
            return;
        }
    }

    logger?.LogWarning(
        "SQLite metadata DB is missing table project_members at {DatabasePath}. Resetting migration record v3 and reapplying.",
        factory.DatabasePath);

    await using (var connection = await factory.OpenAsync(CancellationToken.None))
    await using (var tx = await connection.BeginTransactionAsync(CancellationToken.None))
    {
        await using var del = connection.CreateCommand();
        del.Transaction = (SqliteTransaction)tx;
        del.CommandText = """
            DELETE FROM schema_migrations WHERE store = $store AND version = $version;
            """;
        del.Parameters.AddWithValue("$store", "sqlite_metadata");
        del.Parameters.AddWithValue("$version", 3);
        await del.ExecuteNonQueryAsync(CancellationToken.None);
        await tx.CommitAsync(CancellationToken.None);
    }

    var metadataMigrator = provider
        .GetServices<IDatabaseMigrator>()
        .OfType<SqliteMetadataMigrator>()
        .FirstOrDefault();

    if (metadataMigrator is null)
    {
        throw new InvalidOperationException(
            "SQLite metadata is active but SqliteMetadataMigrator is not registered.");
    }

    await metadataMigrator.MigrateAsync(CancellationToken.None);

    await using (var verify = await factory.OpenAsync(CancellationToken.None))
    {
        await using var verifyCmd = verify.CreateCommand();
        verifyCmd.CommandText = """
            SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'project_members' LIMIT 1;
            """;
        if (await verifyCmd.ExecuteScalarAsync(CancellationToken.None) is null)
        {
            throw new InvalidOperationException(
                $"Не удалось создать таблицу project_members в '{factory.DatabasePath}'. " +
                "Удалите файл базы или строки schema_migrations для store sqlite_metadata и перезапустите приложение.");
        }
    }

    logger?.LogInformation("SQLite metadata tables project_members / project_share_invites are present.");
}
