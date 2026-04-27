using LogAnalyzer.Application;
using LogAnalyzer.Domain;
using LogAnalyzer.Web.Auth;
using Microsoft.AspNetCore.Components;

namespace LogAnalyzer.Web.Components.Pages;

public partial class Home
{
    private CurrentUser currentUser = new("local-dev", "local@dev.local", "Локальный пользователь", true);
    private List<ProjectEntity> projects = [];
    private Dictionary<string, ProjectSummary> summaries = new(StringComparer.OrdinalIgnoreCase);
    private string newName = string.Empty;
    private string? newDescription;
    private string? error;
    private bool isBusy;
    private int logCount;
    private long eventCount;
    private long problemCount;

    [Inject] private IMetadataRepository Metadata { get; set; } = null!;
    [Inject] private ILogEventStore EventStore { get; set; } = null!;
    [Inject] private ICurrentUserService CurrentUserService { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        currentUser = await CurrentUserService.GetAsync(CancellationToken.None);
        await LoadDashboard();
    }

    private async Task LoadDashboard()
    {
        projects = (await Metadata.ListProjectsAsync(currentUser.Id, CancellationToken.None)).ToList();
        summaries.Clear();
        logCount = 0;
        eventCount = 0;
        problemCount = 0;

        foreach (var project in projects)
        {
            var logs = await Metadata.ListLogFilesAsync(project.Id, CancellationToken.None);
            var stats = await EventStore.GetStatsByLogFileAsync(project.Id, CancellationToken.None);
            var projectEvents = stats.Values.Sum(static item => item.EventCount);
            var projectProblems = stats.Values.Sum(static item => item.ErrorCount + item.WarnCount);

            logCount += logs.Count;
            eventCount += projectEvents;
            problemCount += projectProblems;
            summaries[project.Id] = new ProjectSummary(logs.Count, projectEvents, projectProblems);
        }
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

    private sealed record ProjectSummary(int LogCount, long EventCount, long ProblemCount);
}
