using LogAnalyzer.Application;
using LogAnalyzer.Infrastructure;
using LogAnalyzer.Web.Api;
using LogAnalyzer.Web.Components;
using LogAnalyzer.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddLogAnalyzerInfrastructure(builder.Configuration);
builder.Services.AddSingleton<ImportCancellationRegistry>();
builder.Services.AddHostedService<ImportWorkerService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();
app.MapLogAnalyzerApi();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await InitializeStorageAsync(app.Services);

app.Run();

static async Task InitializeStorageAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var metadata = scope.ServiceProvider.GetRequiredService<IMetadataRepository>();
    var events = scope.ServiceProvider.GetRequiredService<ILogEventStore>();

    await metadata.InitializeAsync(CancellationToken.None);
    await events.InitializeAsync(CancellationToken.None);
}
