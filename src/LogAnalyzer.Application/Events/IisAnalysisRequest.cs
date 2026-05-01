namespace LogAnalyzer.Application;

public sealed record IisAnalysisRequest
{
    public required string ProjectId { get; init; }
    public DateTimeOffset? FromUtc { get; init; }
    public DateTimeOffset? ToUtc { get; init; }
    public DateTimeOffset? AroundUtc { get; init; }
    public int BeforeSeconds { get; init; } = EventSearchDefaults.DefaultBeforeSeconds;
    public int AfterSeconds { get; init; } = EventSearchDefaults.DefaultAfterSeconds;
    public IReadOnlyCollection<string> LogFileIds { get; init; } = [];
    public string? HttpMethod { get; init; }
    public string? Url { get; init; }
    public string? ClientIp { get; init; }
    public string? UserName { get; init; }
    public int? StatusCodeClass { get; init; }
    public int? MinTimeTaken { get; init; }
    public int SlowRequestThresholdMs { get; init; } = EventSearchDefaults.DefaultSlowRequestThresholdMs;
    public int TopLimit { get; init; } = EventSearchDefaults.DefaultHttpTopLimit;
}
