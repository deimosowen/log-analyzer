namespace LogAnalyzer.Application;

public sealed record StoredUploadFile(
    string OriginalName,
    string StoredPath,
    long SizeBytes);
