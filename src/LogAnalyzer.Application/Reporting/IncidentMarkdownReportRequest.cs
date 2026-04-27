using LogAnalyzer.Application.Analysis;
using LogAnalyzer.Domain;

namespace LogAnalyzer.Application.Reporting;

public sealed record IncidentMarkdownReportRequest(
    string ProjectName,
    string DisplayTimeZoneId,
    string SelectedLogPreview,
    string? Query,
    int BeforeSeconds,
    int AfterSeconds,
    LogEvent SelectedEvent,
    IReadOnlyList<LogEvent> CorrelatedEvents,
    IReadOnlyList<CorrelationGroup> CorrelationGroups,
    long CorrelatedTotalCount,
    Func<DateTimeOffset, string> FormatTime,
    Func<LogEvent, string> FormatDelta,
    Func<CorrelationGroup, string> FormatGroupWindow,
    Func<string, string> LogName);
