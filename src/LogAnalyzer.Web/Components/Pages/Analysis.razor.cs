using System.Globalization;
using LogAnalyzer.Application;
using LogAnalyzer.Application.Analysis;
using LogAnalyzer.Application.Reporting;
using LogAnalyzer.Application.Time;
using LogAnalyzer.Domain;
using LogAnalyzer.Web.Auth;
using LogAnalyzer.Web.Formatting;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.JSInterop;

namespace LogAnalyzer.Web.Components.Pages;

public partial class Analysis : IAsyncDisposable
{
    private static readonly string[] ProblemLevels = ProblemEventLevels.Default;

    private enum CorrelationDisplayMode
    {
        Summary,
        Events,
        Http
    }

    [Parameter] public string ProjectId { get; set; } = string.Empty;

    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;

    private ProjectEntity? project;
    private CurrentUser currentUser = new("local-dev", "local@dev.local", "Локальный пользователь", true);
    private List<LogFileEntity> logs = [];
    private IReadOnlyDictionary<string, LogFileEventStats> stats = new Dictionary<string, LogFileEventStats>();
    private Dictionary<string, string> logNames = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> selectedLogIds = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> selectedLevels = new(StringComparer.OrdinalIgnoreCase);
    private List<LogEvent> correlatedEvents = [];
    private List<LogEvent> httpDrilldownEvents = [];
    private IReadOnlyList<CorrelationGroup> correlationGroups = [];
    private IncidentTimeline incidentTimeline = IncidentTimeline.Empty;
    private IisAnalysisResult iisAnalysis = IisAnalysisResult.Empty;
    private readonly string timelineChartElementId = $"incident-timeline-{Guid.NewGuid():N}";
    private DotNetObjectReference<Analysis>? timelineChartReference;
    private Virtualize<LogEvent>? problemVirtualize;
    private LogEvent? selectedEvent;
    private long correlatedTotalCount;
    private string displayTimeZoneId = TimeZoneDefaults.Display;
    private string? query;
    private int beforeSeconds = EventSearchDefaults.DefaultBeforeSeconds;
    private int afterSeconds = EventSearchDefaults.DefaultAfterSeconds;
    private string? httpMethodFilter;
    private string? httpUrlFilter;
    private string? httpClientIpFilter;
    private string? httpUserNameFilter;
    private string httpStatusClassFilter = string.Empty;
    private int? httpMinTimeTaken;
    private bool showStep1 = true;
    private bool showStep2 = true;
    private bool showIncidentTimeline;
    private bool hideSuccessfulHttp = true;
    private bool timelineChartDirty;
    private CorrelationDisplayMode correlationMode = CorrelationDisplayMode.Summary;

    private string StageClass => (showStep1, showStep2) switch
    {
        (true, true) => "analysis-stage",
        (false, true) => "analysis-stage only-step2",
        (true, false) => "analysis-stage only-step1",
        _ => "analysis-stage all-steps-hidden"
    };

    private string CorrelationCountLabel => correlationMode switch
    {
        CorrelationDisplayMode.Summary => $"{correlationGroups.Count} групп / {LoadedCorrelationCountLabel}",
        CorrelationDisplayMode.Http => $"{iisAnalysis.Summary.TotalRequests} HTTP-запросов",
        _ => LoadedCorrelationCountLabel
    };

    private string LoadedCorrelationCountLabel => IsCorrelationTruncated
        ? $"{correlatedEvents.Count} из {correlatedTotalCount} событий"
        : $"{correlatedEvents.Count} событий";

    private bool IsCorrelationTruncated => correlatedTotalCount > correlatedEvents.Count;

    private string ReportEditorUrl
    {
        get
        {
            if (selectedEvent is null)
            {
                return string.Empty;
            }

            var selectedLogs = selectedLogIds.Count == logs.Count
                ? null
                : string.Join(",", selectedLogIds);

            return Navigation.GetUriWithQueryParameters($"/projects/{ProjectId}/report", new Dictionary<string, object?>
            {
                ["eventId"] = selectedEvent.Id,
                ["before"] = beforeSeconds,
                ["after"] = afterSeconds,
                ["tz"] = displayTimeZoneId,
                ["query"] = query,
                ["logs"] = selectedLogs,
                ["hideSuccessfulHttp"] = hideSuccessfulHttp
            });
        }
    }

    private string SelectedLogPreview
    {
        get
        {
            if (selectedLogIds.Count == 0)
            {
                return "логи не выбраны";
            }

            if (logs.Count > 0 && selectedLogIds.Count == logs.Count)
            {
                return "все логи";
            }

            var names = logs
                .Where(log => selectedLogIds.Contains(log.Id))
                .Select(LogDisplayName)
                .Take(3)
                .ToArray();

            var suffix = selectedLogIds.Count > names.Length
                ? $" +{selectedLogIds.Count - names.Length}"
                : string.Empty;

            return string.Join(", ", names) + suffix;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        currentUser = await CurrentUserService.GetAsync(CancellationToken.None);
        project = await Metadata.GetProjectAsync(currentUser.Id, ProjectId, CancellationToken.None);
        if (project is null)
        {
            return;
        }

        logs = (await Metadata.ListLogFilesAsync(ProjectId, CancellationToken.None)).ToList();
        logNames = logs.ToDictionary(log => log.Id, LogFileDisplayName.Format, StringComparer.OrdinalIgnoreCase);
        selectedLogIds = logs.Select(log => log.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        selectedLevels = ProblemLevels.ToHashSet(StringComparer.OrdinalIgnoreCase);
        stats = await EventStore.GetStatsByLogFileAsync(ProjectId, CancellationToken.None);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            timelineChartReference = DotNetObjectReference.Create(this);
        }

        if (timelineChartDirty)
        {
            await SynchronizeTimelineChartAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        timelineChartReference?.Dispose();

        try
        {
            await JsRuntime.InvokeVoidAsync("logAnalyzerTimeline.dispose", timelineChartElementId);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private async ValueTask<ItemsProviderResult<LogEvent>> LoadProblems(ItemsProviderRequest request)
    {
        var result = await EventStore.SearchAsync(
            BuildProblemSearchRequest(request.StartIndex, request.Count),
            request.CancellationToken);

        return new ItemsProviderResult<LogEvent>(
            result.Events,
            (int)Math.Min(result.TotalCount, int.MaxValue));
    }

    private LogEventSearchRequest BuildProblemSearchRequest(int offset, int limit)
    {
        return new LogEventSearchRequest
        {
            ProjectId = ProjectId,
            LogFileIds = selectedLogIds.ToArray(),
            Levels = ActiveLevels(),
            Query = query,
            Offset = offset,
            Limit = limit <= 0 ? 100 : limit
        };
    }

    private LogEventSearchRequest BuildCorrelationSearchRequest()
    {
        return new LogEventSearchRequest
        {
            ProjectId = ProjectId,
            AroundUtc = selectedEvent?.TimestampUtc,
            BeforeSeconds = beforeSeconds,
            AfterSeconds = afterSeconds,
            LogFileIds = selectedLogIds.ToArray(),
            Levels = ProblemLevels,
            ExcludeSuccessfulHttp = hideSuccessfulHttp,
            Offset = 0,
            Limit = EventSearchDefaults.CorrelationLimit
        };
    }

    private async Task Refresh()
    {
        await RefreshCorrelation();
        if (problemVirtualize is not null)
        {
            await problemVirtualize.RefreshDataAsync();
        }
    }

    private async Task RefreshCorrelation()
    {
        if (selectedEvent is null)
        {
            ClearCorrelation();
            return;
        }

        var result = await EventStore.SearchAsync(BuildCorrelationSearchRequest(), CancellationToken.None);
        correlatedEvents = result.Events.ToList();
        correlatedTotalCount = result.TotalCount;
        correlationGroups = CorrelationGrouping.GroupProblemEvents(correlatedEvents);
        await RefreshIncidentTimeline();
        await RefreshIisAnalysis();
    }

    private async Task RefreshIncidentTimeline()
    {
        if (selectedEvent is null)
        {
            incidentTimeline = IncidentTimeline.Empty;
            timelineChartDirty = true;
            return;
        }

        var from = selectedEvent.TimestampUtc.AddSeconds(-Math.Max(0, beforeSeconds));
        var to = selectedEvent.TimestampUtc.AddSeconds(Math.Max(0, afterSeconds));
        var bucket = IncidentTimelineBuilder.ChooseBucket(from, to);
        var points = await EventStore.GetTimelineAsync(new TimelineRequest
        {
            ProjectId = ProjectId,
            FromUtc = from,
            ToUtc = to,
            Bucket = bucket,
            LogFileIds = selectedLogIds.ToArray(),
            Levels = ActiveLevels()
        }, CancellationToken.None);

        incidentTimeline = IncidentTimelineBuilder.Build(points, from, to, bucket, selectedEvent.TimestampUtc);
        timelineChartDirty = true;
    }

    private async Task SelectProblem(LogEvent item)
    {
        selectedEvent = item;
        await RefreshCorrelation();
    }

    private async Task ResetView()
    {
        selectedEvent = null;
        ClearCorrelation();
        query = null;
        selectedLevels = ProblemLevels.ToHashSet(StringComparer.OrdinalIgnoreCase);
        selectedLogIds = logs.Select(log => log.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        beforeSeconds = EventSearchDefaults.DefaultBeforeSeconds;
        afterSeconds = EventSearchDefaults.DefaultAfterSeconds;
        await Refresh();
    }

    private void ClearCorrelation()
    {
        correlatedEvents.Clear();
        httpDrilldownEvents.Clear();
        correlationGroups = [];
        correlatedTotalCount = 0;
        incidentTimeline = IncidentTimeline.Empty;
        iisAnalysis = IisAnalysisResult.Empty;
        timelineChartDirty = true;
    }

    private void ToggleStep1()
    {
        showStep1 = !showStep1;
    }

    private void ToggleStep2()
    {
        showStep2 = !showStep2;
    }

    private void ToggleIncidentTimeline()
    {
        showIncidentTimeline = !showIncidentTimeline;
        timelineChartDirty = true;
    }

    private void ShowCorrelationSummary()
    {
        correlationMode = CorrelationDisplayMode.Summary;
    }

    private void ShowCorrelationEvents()
    {
        correlationMode = CorrelationDisplayMode.Events;
    }

    private async Task ShowHttpAnalysis()
    {
        correlationMode = CorrelationDisplayMode.Http;
        await RefreshIisAnalysis();
    }

    private async Task RefreshIisAnalysis()
    {
        if (selectedEvent is null)
        {
            iisAnalysis = IisAnalysisResult.Empty;
            httpDrilldownEvents.Clear();
            return;
        }

        iisAnalysis = await EventStore.GetIisAnalysisAsync(BuildIisAnalysisRequest(), CancellationToken.None);
    }

    private IisAnalysisRequest BuildIisAnalysisRequest()
    {
        return new IisAnalysisRequest
        {
            ProjectId = ProjectId,
            AroundUtc = selectedEvent?.TimestampUtc,
            BeforeSeconds = beforeSeconds,
            AfterSeconds = afterSeconds,
            LogFileIds = selectedLogIds.ToArray(),
            HttpMethod = httpMethodFilter,
            Url = httpUrlFilter,
            ClientIp = httpClientIpFilter,
            UserName = httpUserNameFilter,
            StatusCodeClass = HttpStatusClassFilter(),
            MinTimeTaken = httpMinTimeTaken,
            SlowRequestThresholdMs = httpMinTimeTaken ?? EventSearchDefaults.DefaultSlowRequestThresholdMs,
            TopLimit = EventSearchDefaults.DefaultHttpTopLimit
        };
    }

    private async Task ApplyHttpFilters()
    {
        httpDrilldownEvents.Clear();
        await RefreshIisAnalysis();
    }

    private async Task ResetHttpFilters()
    {
        httpMethodFilter = null;
        httpUrlFilter = null;
        httpClientIpFilter = null;
        httpUserNameFilter = null;
        httpStatusClassFilter = string.Empty;
        httpMinTimeTaken = null;
        await ApplyHttpFilters();
    }

    private async Task OpenHttpAggregate(IisEndpointAggregate aggregate)
    {
        var result = await EventStore.SearchAsync(new LogEventSearchRequest
        {
            ProjectId = ProjectId,
            AroundUtc = selectedEvent?.TimestampUtc,
            BeforeSeconds = beforeSeconds,
            AfterSeconds = afterSeconds,
            LogFileIds = selectedLogIds.ToArray(),
            OnlyHttp = true,
            HttpMethod = aggregate.Method,
            Url = aggregate.Url,
            StatusCodeClass = aggregate.StatusCodeClass,
            ClientIp = httpClientIpFilter,
            UserName = httpUserNameFilter,
            MinTimeTaken = httpMinTimeTaken,
            Offset = 0,
            Limit = EventSearchDefaults.HttpDrilldownLimit
        }, CancellationToken.None);

        httpDrilldownEvents = result.Events.ToList();
    }

    private async Task FocusTimelineBucket(IncidentTimelineBucket bucket)
    {
        if (selectedEvent is null || bucket.TotalCount == 0)
        {
            return;
        }

        beforeSeconds = Math.Max(0, (int)Math.Ceiling((selectedEvent.TimestampUtc - bucket.BucketUtc).TotalSeconds));
        afterSeconds = Math.Max(0, (int)Math.Ceiling((bucket.BucketEndUtc - selectedEvent.TimestampUtc).TotalSeconds));
        correlationMode = CorrelationDisplayMode.Events;
        await RefreshCorrelation();
    }

    [JSInvokable]
    public async Task FocusTimelineBucketFromChart(string bucketUtc)
    {
        if (!DateTimeOffset.TryParse(
                bucketUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var selectedBucketUtc))
        {
            return;
        }

        var bucket = incidentTimeline.Buckets.FirstOrDefault(
            item => item.BucketUtc.ToUniversalTime() == selectedBucketUtc.ToUniversalTime());

        if (bucket is null)
        {
            return;
        }

        await FocusTimelineBucket(bucket);
        await InvokeAsync(StateHasChanged);
    }

    private void BackToProject()
    {
        Navigation.NavigateTo($"/projects/{ProjectId}");
    }

    private void BackToProjects()
    {
        Navigation.NavigateTo("/");
    }

    private async Task ToggleLog(string logFileId, ChangeEventArgs args)
    {
        if (args.Value is bool selected && selected)
        {
            selectedLogIds.Add(logFileId);
        }
        else
        {
            selectedLogIds.Remove(logFileId);
        }

        await Refresh();
    }

    private async Task ToggleLevel(string level, ChangeEventArgs args)
    {
        if (args.Value is bool selected && selected)
        {
            selectedLevels.Add(level);
        }
        else
        {
            selectedLevels.Remove(level);
        }

        await Refresh();
    }

    private string[] ActiveLevels()
    {
        return selectedLevels.Count == 0
            ? ProblemLevels
            : selectedLevels.ToArray();
    }

    private string FormatTime(DateTimeOffset timestampUtc)
    {
        try
        {
            var zone = TimeZoneHelper.Find(displayTimeZoneId);
            return TimeZoneInfo.ConvertTime(timestampUtc, zone).ToString("yyyy-MM-dd HH:mm:ss.ffff");
        }
        catch
        {
            return timestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.ffff");
        }
    }

    private string FormatDelta(LogEvent item)
    {
        if (selectedEvent is null)
        {
            return "";
        }

        return FormatSignedSeconds(item.TimestampUtc - selectedEvent.TimestampUtc);
    }

    private string LogName(string id)
    {
        return logNames.TryGetValue(id, out var name) ? name : id;
    }

    private static string LogDisplayName(LogFileEntity log)
    {
        return LogFileDisplayName.Format(log);
    }

    private string FormatGroupWindow(CorrelationGroup group)
    {
        if (selectedEvent is null)
        {
            return string.Empty;
        }

        if (group.Count == 1)
        {
            return FormatDelta(group.Representative);
        }

        return $"{FormatSignedSeconds(group.FirstTimestamp - selectedEvent.TimestampUtc)}..{FormatSignedSeconds(group.LastTimestamp - selectedEvent.TimestampUtc)}";
    }

    private static string FormatSignedSeconds(TimeSpan delta)
    {
        return delta.TotalMilliseconds switch
        {
            > 0 => $"+{delta.TotalSeconds:0.###}s",
            < 0 => $"{delta.TotalSeconds:0.###}s",
            _ => "0s"
        };
    }

    private static string DisplayLevel(string level)
    {
        return string.IsNullOrWhiteSpace(level) ? "RAW" : level;
    }

    private int? HttpStatusClassFilter()
    {
        return int.TryParse(httpStatusClassFilter, CultureInfo.InvariantCulture, out var statusClass)
            ? statusClass
            : null;
    }

    private static string FormatHttpStatusClass(int statusCodeClass)
    {
        return statusCodeClass > 0 ? $"{statusCodeClass}xx" : "HTTP";
    }

    private static string FormatMilliseconds(int value)
    {
        return value >= 1000 ? $"{value / 1000d:0.##} c" : $"{value} мс";
    }

    private static string FormatCount(long value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture).Replace(",", " ", StringComparison.Ordinal);
    }

    private async Task SynchronizeTimelineChartAsync()
    {
        if (!showIncidentTimeline || selectedEvent is null || incidentTimeline.IsEmpty)
        {
            await JsRuntime.InvokeVoidAsync("logAnalyzerTimeline.dispose", timelineChartElementId);
            timelineChartDirty = false;
            return;
        }

        timelineChartReference ??= DotNetObjectReference.Create(this);
        var rendered = await JsRuntime.InvokeAsync<bool>(
            "logAnalyzerTimeline.render",
            timelineChartElementId,
            BuildTimelineChartPayload(),
            timelineChartReference);

        timelineChartDirty = !rendered;
    }

    private object BuildTimelineChartPayload()
    {
        return new
        {
            bucketLabel = FormatBucket(incidentTimeline.Bucket),
            totalCount = incidentTimeline.TotalCount,
            peakCount = incidentTimeline.MaxBucketCount,
            buckets = incidentTimeline.Buckets.Select(bucket => new
            {
                bucketUtc = bucket.BucketUtc.ToString("O", CultureInfo.InvariantCulture),
                label = FormatChartBucketLabel(bucket.BucketUtc),
                fullLabel = FormatTime(bucket.BucketUtc),
                fatal = bucket.FatalCount,
                error = bucket.ErrorCount,
                warn = bucket.WarnCount,
                info = bucket.InfoCount,
                other = bucket.OtherCount,
                total = bucket.TotalCount,
                anchor = bucket.ContainsAnchor
            }).ToArray()
        };
    }

    private string FormatChartBucketLabel(DateTimeOffset timestampUtc)
    {
        try
        {
            var zone = TimeZoneHelper.Find(displayTimeZoneId);
            var local = TimeZoneInfo.ConvertTime(timestampUtc, zone);
            return incidentTimeline.Bucket.TotalMinutes >= 1
                ? local.ToString("HH:mm", CultureInfo.InvariantCulture)
                : local.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }
        catch
        {
            var local = timestampUtc.ToLocalTime();
            return incidentTimeline.Bucket.TotalMinutes >= 1
                ? local.ToString("HH:mm", CultureInfo.InvariantCulture)
                : local.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }

    private static string FormatBucket(TimeSpan bucket)
    {
        if (bucket.TotalMinutes >= 1)
        {
            return $"{bucket.TotalMinutes:0} мин";
        }

        return $"{bucket.TotalSeconds:0} сек";
    }
}
