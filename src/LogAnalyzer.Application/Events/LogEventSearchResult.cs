using LogAnalyzer.Domain;

namespace LogAnalyzer.Application;

public sealed record LogEventSearchResult(
    IReadOnlyList<LogEvent> Events,
    long TotalCount);
