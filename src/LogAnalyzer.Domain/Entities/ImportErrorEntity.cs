namespace LogAnalyzer.Domain;

public sealed record ImportErrorEntity(
    string Id,
    string UploadSessionId,
    string? LogFileId,
    long? LineNumber,
    string ErrorMessage,
    string? RawText,
    DateTimeOffset CreatedAt);
