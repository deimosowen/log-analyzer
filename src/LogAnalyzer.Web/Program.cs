using LogAnalyzer.Application;
using LogAnalyzer.Application.Dashboard;
using LogAnalyzer.Infrastructure;
using LogAnalyzer.Infrastructure.Migrations;
using LogAnalyzer.Infrastructure.Storage;
using LogAnalyzer.Web.Api;
using LogAnalyzer.Web.Auth;
using LogAnalyzer.Web.Components;
using LogAnalyzer.Web.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;

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
    var migrators = scope.ServiceProvider.GetServices<IDatabaseMigrator>();

    foreach (var migrator in migrators)
    {
        await migrator.MigrateAsync(CancellationToken.None);
    }
}
