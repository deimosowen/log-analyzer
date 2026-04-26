namespace LogAnalyzer.Application;

public sealed record UploadSessionCreateRequest(
    string ProjectId,
    string? OriginalName);
