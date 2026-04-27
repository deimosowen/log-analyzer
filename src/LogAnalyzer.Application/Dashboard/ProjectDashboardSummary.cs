using LogAnalyzer.Domain;

namespace LogAnalyzer.Application.Dashboard;

public sealed record ProjectDashboardSummary(
    ProjectEntity Project,
    int UploadCount,
    int CompletedUploadCount,
    int FailedUploadCount,
    int LogCount,
    int AnalyzedLogCount,
    long LineCount,
    long EventCount,
    long ProblemCount,
    long ImportErrorCount,
    DateTimeOffset LastActivityAt);
