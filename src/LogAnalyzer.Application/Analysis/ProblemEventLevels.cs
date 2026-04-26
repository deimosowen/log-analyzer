using LogAnalyzer.Domain;

namespace LogAnalyzer.Application.Analysis;

public static class ProblemEventLevels
{
    public static readonly string[] Default = [LogLevels.Error, LogLevels.Warn, LogLevels.Fatal];
}
