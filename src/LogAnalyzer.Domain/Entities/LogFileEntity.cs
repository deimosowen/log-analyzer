namespace LogAnalyzer.Domain;

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
