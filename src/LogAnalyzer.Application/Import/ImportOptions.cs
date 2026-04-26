namespace LogAnalyzer.Application;

public sealed record ImportOptions(
    string TimeZoneId,
    bool IisLogsAreUtc,
    bool CombineMultilineExceptions);
