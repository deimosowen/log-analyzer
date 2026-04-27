using System.Globalization;
using LogAnalyzer.Application;
using LogAnalyzer.Application.Dashboard;
using LogAnalyzer.Web.Auth;
using Microsoft.AspNetCore.Components;

namespace LogAnalyzer.Web.Components.Pages;

public partial class Home
{
    private CurrentUser currentUser = new("local-dev", "local@dev.local", "Локальный пользователь", true);
    private UserDashboardSummary dashboard = UserDashboardSummary.Empty;
    private string newName = string.Empty;
    private string? newDescription;
    private string? error;
    private bool isBusy;

    [Inject] private IMetadataRepository Metadata { get; set; } = null!;
    [Inject] private UserDashboardService DashboardService { get; set; } = null!;
    [Inject] private ICurrentUserService CurrentUserService { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        currentUser = await CurrentUserService.GetAsync(CancellationToken.None);
        await LoadDashboard();
    }

    private async Task LoadDashboard()
    {
        dashboard = await DashboardService.GetForUserAsync(currentUser.Id, CancellationToken.None);
    }

    private async Task CreateProject()
    {
        error = null;
        if (string.IsNullOrWhiteSpace(newName))
        {
            error = "Укажите название проекта.";
            return;
        }

        isBusy = true;
        try
        {
            var project = await Metadata.CreateProjectAsync(currentUser.Id, newName, newDescription, CancellationToken.None);
            newName = string.Empty;
            newDescription = null;
            Navigation.NavigateTo($"/projects/{project.Id}");
        }
        finally
        {
            isBusy = false;
        }
    }

    private void OpenProject(string projectId)
    {
        Navigation.NavigateTo($"/projects/{projectId}");
    }

    private async Task DeleteProject(string projectId)
    {
        await Metadata.DeleteProjectAsync(currentUser.Id, projectId, CancellationToken.None);
        await LoadDashboard();
    }

    private static string FormatCount(long value)
    {
        return value.ToString("N0", CultureInfo.GetCultureInfo("ru-RU"));
    }

    private static string FormatLocalDate(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }
}
