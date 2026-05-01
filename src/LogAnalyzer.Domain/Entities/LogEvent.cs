namespace LogAnalyzer.Domain;

public sealed record LogEvent
{
    public required string Id { get; init; }
    public required string ProjectId { get; init; }
    public required string UploadSessionId { get; init; }
    public required string LogFileId { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    public required long TimestampMs { get; init; }
    public string Level { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string ThreadId { get; init; } = string.Empty;
    public long LineNumber { get; init; }
    public long EndLineNumber { get; init; }
    public long ByteOffset { get; init; }
    public string Message { get; init; } = string.Empty;
    public string Exception { get; init; } = string.Empty;
    public string RawText { get; init; } = string.Empty;
    public string HttpMethod { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public int StatusCode { get; init; }
    public string ClientIp { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string ServerIp { get; init; } = string.Empty;
    public string UserAgent { get; init; } = string.Empty;
    public int TimeTaken { get; init; }
}
