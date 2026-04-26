namespace LogAnalyzer.Application.Parsing;

public sealed record LogParserContext(
    string ProjectId,
    string UploadSessionId,
    string LogFileId,
    string TimeZoneId,
    bool IisLogsAreUtc,
    bool CombineMultilineExceptions);
