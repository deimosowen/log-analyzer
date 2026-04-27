using LogAnalyzer.Application;
using LogAnalyzer.Application.Time;
using LogAnalyzer.Domain;
using LogAnalyzer.Infrastructure.Storage;
using LogAnalyzer.Web.Auth;
using LogAnalyzer.Web.Formatting;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.Web.Components.Pages;

public partial class Project : IAsyncDisposable
{
    private ProjectEntity? project;
    private CurrentUser currentUser = new("local-dev", "local@dev.local", "Локальный пользователь", true);
    private List<UploadSessionEntity> uploads = [];
    private List<LogFileEntity> logs = [];
    private IReadOnlyDictionary<string, LogFileEventStats> stats = new Dictionary<string, LogFileEventStats>();
    private IReadOnlyList<IBrowserFile> selectedFiles = [];
    private string timeZoneId = TimeZoneDefaults.Utc;
    private bool iisLogsAreUtc = true;
    private bool combineMultiline = true;
    private bool uploadBusy;
    private string? error;
    private readonly CancellationTokenSource refreshCts = new();

    [Parameter] public string ProjectId { get; set; } = string.Empty;

    [Inject] private IMetadataRepository Metadata { get; set; } = null!;
    [Inject] private ILogFileStorage Storage { get; set; } = null!;
    [Inject] private ILogEventStore EventStore { get; set; } = null!;
    [Inject] private IImportJobQueue ImportQueue { get; set; } = null!;
    [Inject] private ICurrentUserService CurrentUserService { get; set; } = null!;
    [Inject] private IOptions<StorageOptions> StorageOptions { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;

    private IEnumerable<LogFileEntity> FilteredLogs => logs.OrderBy(LogFileDisplayName.Format);

    protected override async Task OnInitializedAsync()
    {
        currentUser = await CurrentUserService.GetAsync(CancellationToken.None);
        await LoadData();
        _ = RefreshLoop();
    }

    private async Task LoadData()
    {
        project = await Metadata.GetProjectAsync(currentUser.Id, ProjectId, CancellationToken.None);
        if (project is null)
        {
            return;
        }

        uploads = (await Metadata.ListUploadSessionsAsync(ProjectId, CancellationToken.None)).ToList();
        logs = (await Metadata.ListLogFilesAsync(ProjectId, CancellationToken.None)).ToList();
        stats = await EventStore.GetStatsByLogFileAsync(ProjectId, CancellationToken.None);
    }

    private void OnFilesSelected(InputFileChangeEventArgs args)
    {
        selectedFiles = args.GetMultipleFiles(1000);
        error = null;
    }

    private async Task StartUpload()
    {
        error = null;
        uploadBusy = true;
        try
        {
            var upload = await Metadata.CreateUploadSessionAsync(
                new UploadSessionCreateRequest(ProjectId, string.Join(", ", selectedFiles.Select(file => file.Name))),
                CancellationToken.None);

            await Metadata.UpdateUploadSessionAsync(upload.Id, new UploadProgressUpdate(Status: UploadStatuses.Uploading), CancellationToken.None);
            foreach (var file in selectedFiles)
            {
                await using var stream = file.OpenReadStream(maxAllowedSize: StorageOptions.Value.MaxUploadBytes);
                await Storage.SaveOriginalAsync(ProjectId, upload.Id, file.Name, stream, CancellationToken.None);
            }

            await Metadata.UpdateUploadSessionAsync(upload.Id, new UploadProgressUpdate(Status: UploadStatuses.Uploaded), CancellationToken.None);
            await ImportQueue.EnqueueAsync(
                new ImportJob(ProjectId, upload.Id, new ImportOptions(timeZoneId, iisLogsAreUtc, combineMultiline)),
                CancellationToken.None);

            selectedFiles = [];
            await LoadData();
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        finally
        {
            uploadBusy = false;
        }
    }

    private void OpenAnalysis()
    {
        Navigation.NavigateTo($"/projects/{ProjectId}/analysis");
    }

    private void BackToProjects()
    {
        Navigation.NavigateTo("/");
    }

    private async Task RefreshLoop()
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (await timer.WaitForNextTickAsync(refreshCts.Token))
            {
                await InvokeAsync(async () =>
                {
                    await LoadData();
                    StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        await refreshCts.CancelAsync();
        refreshCts.Dispose();
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }

    private static string FormatTime(DateTimeOffset? value)
    {
        return value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.ffff") ?? "";
    }

    private static string DisplayStatus(string status)
    {
        return status switch
        {
            UploadStatuses.Created => "создан",
            UploadStatuses.Uploading => "загрузка",
            UploadStatuses.Uploaded => "загружен",
            UploadStatuses.Indexing => "индексация",
            UploadStatuses.Completed => "готово",
            UploadStatuses.Failed => "ошибка",
            UploadStatuses.Cancelled => "отменен",
            LogFileStatuses.Unknown => "неизвестно",
            _ => status
        };
    }
}
