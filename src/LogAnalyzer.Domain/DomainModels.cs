namespace LogAnalyzer.Domain;

public static class UploadStatuses
{
    public const string Created = "created";
    public const string Uploading = "uploading";
    public const string Uploaded = "uploaded";
    public const string Indexing = "indexing";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

public static class LogFileStatuses
{
    public const string Created = "created";
    public const string Indexing = "indexing";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Unknown = "unknown";
}

public static class LogFormats
{
    public const string AppPipeLog = "app_pipe_log";
    public const string NLogPlain = "nlog_plain";
    public const string IisW3C = "iis_w3c";
    public const string Unknown = "unknown";
}

public static class LogLevels
{
    public static readonly string[] Known =
    [
        "ERROR",
        "WARN",
        "INFO",
        "DEBUG",
        "TRACE",
        "FATAL"
    ];

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToUpperInvariant();
        return normalized switch
        {
            "WARNING" => "WARN",
            "ERR" => "ERROR",
            _ => normalized
        };
    }
}

public sealed record ProjectEntity(
    string Id,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record UploadSessionEntity(
    string Id,
    string ProjectId,
    string Status,
    string? OriginalName,
    int TotalFiles,
    int ProcessedFiles,
    long TotalLines,
    long ProcessedLines,
    int ErrorCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinishedAt,
    string? CurrentFile);

public sealed record LogFileEntity(
    string Id,
    string ProjectId,
    string UploadSessionId,
    string OriginalPath,
    string StoredPath,
    string DisplayName,
    long SizeBytes,
    string? Hash,
    string Format,
    string TimeZone,
    DateTimeOffset? FirstTimestamp,
    DateTimeOffset? LastTimestamp,
    long LineCount,
    string Status);

public sealed record ImportErrorEntity(
    string Id,
    string UploadSessionId,
    string? LogFileId,
    long? LineNumber,
    string ErrorMessage,
    string? RawText,
    DateTimeOffset CreatedAt);

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
    public string ServerIp { get; init; } = string.Empty;
    public string UserAgent { get; init; } = string.Empty;
    public int TimeTaken { get; init; }
}

public sealed record LogFileEventStats(
    string LogFileId,
    long EventCount,
    long ErrorCount,
    long WarnCount,
    DateTimeOffset? FirstTimestamp,
    DateTimeOffset? LastTimestamp);

public sealed record TimelinePoint(
    DateTimeOffset BucketUtc,
    string Level,
    long Count);
