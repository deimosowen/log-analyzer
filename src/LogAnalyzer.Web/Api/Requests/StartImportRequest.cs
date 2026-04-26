namespace LogAnalyzer.Web.Api;

public sealed record StartImportRequest(
    string TimeZoneId,
    bool IisLogsAreUtc,
    bool CombineMultilineExceptions);
