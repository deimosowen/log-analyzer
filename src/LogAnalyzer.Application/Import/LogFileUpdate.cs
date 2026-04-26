namespace LogAnalyzer.Application;

public sealed record LogFileUpdate(
    string? Format = null,
    string? Status = null,
    DateTimeOffset? FirstTimestamp = null,
    DateTimeOffset? LastTimestamp = null,
    long? LineCount = null);
