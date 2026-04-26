namespace LogAnalyzer.Application.Parsing;

public sealed record LogSample(
    string FileName,
    IReadOnlyList<string> Lines);
