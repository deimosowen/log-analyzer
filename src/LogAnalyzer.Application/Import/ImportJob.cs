namespace LogAnalyzer.Application;

public sealed record ImportJob(
    string ProjectId,
    string UploadSessionId,
    ImportOptions Options);
