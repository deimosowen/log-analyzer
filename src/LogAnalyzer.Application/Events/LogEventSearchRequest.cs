namespace LogAnalyzer.Application;

public sealed record LogEventSearchRequest
{
    public required string ProjectId { get; init; }
    public DateTimeOffset? FromUtc { get; init; }
    public DateTimeOffset? ToUtc { get; init; }
    public DateTimeOffset? AroundUtc { get; init; }
    public int BeforeSeconds { get; init; } = EventSearchDefaults.DefaultBeforeSeconds;
    public int AfterSeconds { get; init; } = EventSearchDefaults.DefaultAfterSeconds;
    public IReadOnlyCollection<string> LogFileIds { get; init; } = [];
    public IReadOnlyCollection<string> Levels { get; init; } = [];
    public string? Query { get; init; }
    public string? ThreadId { get; init; }
    public string? Source { get; init; }
    public string? HttpMethod { get; init; }
    public string? Url { get; init; }
    public int? StatusCodeClass { get; init; }
    public int Offset { get; init; } = EventSearchDefaults.DefaultOffset;
    public int Limit { get; init; } = EventSearchDefaults.DefaultLimit;
}
