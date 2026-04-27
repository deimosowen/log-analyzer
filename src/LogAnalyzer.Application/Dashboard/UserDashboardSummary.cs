namespace LogAnalyzer.Application.Dashboard;

public sealed record UserDashboardSummary(IReadOnlyList<ProjectDashboardSummary> Projects)
{
    public static UserDashboardSummary Empty { get; } = new([]);

    public int IncidentCount => Projects.Count;
    public int UploadCount => Projects.Sum(static project => project.UploadCount);
    public int CompletedUploadCount => Projects.Sum(static project => project.CompletedUploadCount);
    public int FailedUploadCount => Projects.Sum(static project => project.FailedUploadCount);
    public int LogCount => Projects.Sum(static project => project.LogCount);
    public int AnalyzedLogCount => Projects.Sum(static project => project.AnalyzedLogCount);
    public long LineCount => Projects.Sum(static project => project.LineCount);
    public long EventCount => Projects.Sum(static project => project.EventCount);
    public long ProblemCount => Projects.Sum(static project => project.ProblemCount);
    public long ImportErrorCount => Projects.Sum(static project => project.ImportErrorCount);
    public DateTimeOffset? LastActivityAt => Projects.Count == 0
        ? null
        : Projects.Max(static project => project.LastActivityAt);
}
