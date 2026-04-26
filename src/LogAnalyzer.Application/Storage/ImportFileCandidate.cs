namespace LogAnalyzer.Application;

public sealed record ImportFileCandidate(
    string OriginalPath,
    string StoredPath,
    string DisplayName,
    long SizeBytes,
    string Hash);
