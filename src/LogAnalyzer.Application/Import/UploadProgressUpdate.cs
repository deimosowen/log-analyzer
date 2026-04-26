namespace LogAnalyzer.Application;

public sealed record UploadProgressUpdate(
    string? Status = null,
    int? TotalFiles = null,
    int? ProcessedFiles = null,
    long? TotalLines = null,
    long? ProcessedLines = null,
    int? ErrorCount = null,
    DateTimeOffset? FinishedAt = null,
    string? CurrentFile = null);
