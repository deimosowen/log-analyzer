using LogAnalyzer.Application;
using LogAnalyzer.Application.Analysis;
using LogAnalyzer.Application.Time;
using LogAnalyzer.Domain;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;

namespace LogAnalyzer.Web.Components.Pages;

public partial class Analysis
{
    private const int CorrelationSearchLimit = 1000;

    private static readonly string[] ProblemLevels = ProblemEventLevels.Default;

    private enum CorrelationDisplayMode
    {
        Summary,
        Events
    }

    [Parameter] public string ProjectId { get; set; } = string.Empty;

    private ProjectEntity? project;
    private List<LogFileEntity> logs = [];
    private IReadOnlyDictionary<string, LogFileEventStats> stats = new Dictionary<string, LogFileEventStats>();
    private Dictionary<string, string> logNames = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> selectedLogIds = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> selectedLevels = new(StringComparer.OrdinalIgnoreCase);
    private List<LogEvent> correlatedEvents = [];
    private IReadOnlyList<CorrelationGroup> correlationGroups = [];
    private Virtualize<LogEvent>? problemVirtualize;
    private LogEvent? selectedEvent;
    private long correlatedTotalCount;
    private string displayTimeZoneId = "Europe/Moscow";
    private string? query;
    private int beforeSeconds = 30;
    private int afterSeconds = 10;
    private bool showStep1 = true;
    private bool showStep2 = true;
    private CorrelationDisplayMode correlationMode = CorrelationDisplayMode.Summary;

    private string StageClass => (showStep1, showStep2) switch
    {
        (true, true) => "analysis-stage",
        (false, true) => "analysis-stage only-step2",
        (true, false) => "analysis-stage only-step1",
        _ => "analysis-stage all-steps-hidden"
    };

    private string CorrelationCountLabel => correlationMode == CorrelationDisplayMode.Summary
        ? $"{correlationGroups.Count} групп / {LoadedCorrelationCountLabel}"
        : LoadedCorrelationCountLabel;

    private string LoadedCorrelationCountLabel => IsCorrelationTruncated
        ? $"{correlatedEvents.Count} из {correlatedTotalCount} событий"
        : $"{correlatedEvents.Count} событий";

    private bool IsCorrelationTruncated => correlatedTotalCount > correlatedEvents.Count;

    protected override async Task OnInitializedAsync()
    {
        project = await Metadata.GetProjectAsync(ProjectId, CancellationToken.None);
        logs = (await Metadata.ListLogFilesAsync(ProjectId, CancellationToken.None)).ToList();
        logNames = logs.ToDictionary(log => log.Id, log => log.DisplayName, StringComparer.OrdinalIgnoreCase);
        selectedLogIds = logs.Select(log => log.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        selectedLevels = ProblemLevels.ToHashSet(StringComparer.OrdinalIgnoreCase);
        stats = await EventStore.GetStatsByLogFileAsync(ProjectId, CancellationToken.None);
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
            Offset = 0,
            Limit = CorrelationSearchLimit
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
        beforeSeconds = 30;
        afterSeconds = 10;
        await Refresh();
    }

    private void ClearCorrelation()
    {
        correlatedEvents.Clear();
        correlationGroups = [];
        correlatedTotalCount = 0;
    }

    private void ToggleStep1()
    {
        showStep1 = !showStep1;
    }

    private void ToggleStep2()
    {
        showStep2 = !showStep2;
    }

    private void ShowCorrelationSummary()
    {
        correlationMode = CorrelationDisplayMode.Summary;
    }

    private void ShowCorrelationEvents()
    {
        correlationMode = CorrelationDisplayMode.Events;
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
}
