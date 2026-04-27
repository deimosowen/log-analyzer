using LogAnalyzer.Domain;

namespace LogAnalyzer.Application.Dashboard;

public sealed class UserDashboardService
{
    private readonly IMetadataRepository _metadata;
    private readonly ILogEventStore _events;

    public UserDashboardService(IMetadataRepository metadata, ILogEventStore events)
    {
        _metadata = metadata;
        _events = events;
    }

    public async Task<UserDashboardSummary> GetForUserAsync(string ownerUserId, CancellationToken cancellationToken)
    {
        var projects = await _metadata.ListProjectsAsync(ownerUserId, cancellationToken);
        var projectSummaries = new List<ProjectDashboardSummary>(projects.Count);

        foreach (var project in projects)
        {
            var uploads = await _metadata.ListUploadSessionsAsync(project.Id, cancellationToken);
            var logs = await _metadata.ListLogFilesAsync(project.Id, cancellationToken);
            var stats = await _events.GetStatsByLogFileAsync(project.Id, cancellationToken);

            projectSummaries.Add(BuildProjectSummary(project, uploads, logs, stats));
        }

        return new UserDashboardSummary(projectSummaries);
    }

    private static ProjectDashboardSummary BuildProjectSummary(
        ProjectEntity project,
        IReadOnlyCollection<UploadSessionEntity> uploads,
        IReadOnlyCollection<LogFileEntity> logs,
        IReadOnlyDictionary<string, LogFileEventStats> stats)
    {
        return new ProjectDashboardSummary(
            project,
            uploads.Count,
            CountUploadsByStatus(uploads, UploadStatuses.Completed),
            CountUploadsByStatus(uploads, UploadStatuses.Failed),
            logs.Count,
            logs.Count(static log => string.Equals(log.Status, LogFileStatuses.Completed, StringComparison.OrdinalIgnoreCase)),
            logs.Sum(static log => log.LineCount),
            stats.Values.Sum(static item => item.EventCount),
            stats.Values.Sum(static item => item.ErrorCount + item.WarnCount),
            uploads.Sum(static item => item.ErrorCount),
            GetLastActivity(project, uploads));
    }

    private static int CountUploadsByStatus(IEnumerable<UploadSessionEntity> uploads, string status)
    {
        return uploads.Count(upload => string.Equals(upload.Status, status, StringComparison.OrdinalIgnoreCase));
    }

    private static DateTimeOffset GetLastActivity(ProjectEntity project, IEnumerable<UploadSessionEntity> uploads)
    {
        var lastActivity = project.UpdatedAt;
        foreach (var upload in uploads)
        {
            var uploadActivity = upload.FinishedAt ?? upload.CreatedAt;
            if (uploadActivity > lastActivity)
            {
                lastActivity = uploadActivity;
            }
        }

        return lastActivity;
    }
}
