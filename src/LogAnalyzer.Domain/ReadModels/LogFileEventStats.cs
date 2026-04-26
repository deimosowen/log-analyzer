namespace LogAnalyzer.Domain;

public sealed record LogFileEventStats(
    string LogFileId,
    long EventCount,
    long ErrorCount,
    long WarnCount,
    DateTimeOffset? FirstTimestamp,
    DateTimeOffset? LastTimestamp);
