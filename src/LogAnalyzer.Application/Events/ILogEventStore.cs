using LogAnalyzer.Domain;

namespace LogAnalyzer.Application;

public interface ILogEventStore
{
    Task InsertBatchAsync(IReadOnlyCollection<LogEvent> events, CancellationToken cancellationToken);
    Task<LogEventSearchResult> SearchAsync(LogEventSearchRequest request, CancellationToken cancellationToken);
    Task<LogEvent?> GetEventAsync(string eventId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<string, LogFileEventStats>> GetStatsByLogFileAsync(string projectId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TimelinePoint>> GetTimelineAsync(TimelineRequest request, CancellationToken cancellationToken);
    Task<IisAnalysisResult> GetIisAnalysisAsync(IisAnalysisRequest request, CancellationToken cancellationToken);
}
