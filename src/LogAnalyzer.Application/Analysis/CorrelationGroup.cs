using LogAnalyzer.Domain;

namespace LogAnalyzer.Application.Analysis;

public sealed record CorrelationGroup(
    string Key,
    string Title,
    string Level,
    int Count,
    int LogCount,
    DateTimeOffset FirstTimestamp,
    DateTimeOffset LastTimestamp,
    LogEvent Representative);
