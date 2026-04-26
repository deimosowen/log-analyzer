namespace LogAnalyzer.Application;

public sealed record TimelineRequest
{
    public required string ProjectId { get; init; }
    public required DateTimeOffset FromUtc { get; init; }
    public required DateTimeOffset ToUtc { get; init; }
    public required TimeSpan Bucket { get; init; }
    public IReadOnlyCollection<string> LogFileIds { get; init; } = [];
    public IReadOnlyCollection<string> Levels { get; init; } = [];
}
