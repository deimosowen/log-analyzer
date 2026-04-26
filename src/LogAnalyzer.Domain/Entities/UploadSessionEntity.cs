namespace LogAnalyzer.Domain;

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
