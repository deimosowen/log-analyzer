namespace LogAnalyzer.Web.Api;

public static class ApiRoutes
{
    public const string Root = "/api";
    public const string Projects = "/projects";
    public const string Project = "/projects/{projectId}";
    public const string ProjectUploads = "/projects/{projectId}/uploads";
    public const string UploadFiles = "/uploads/{uploadId}/files";
    public const string UploadStart = "/uploads/{uploadId}/start";
    public const string UploadStatus = "/uploads/{uploadId}/status";
    public const string UploadCancel = "/uploads/{uploadId}/cancel";
    public const string ProjectLogs = "/projects/{projectId}/logs";
    public const string LogEvents = "/logs/{logFileId}/events";
    public const string Event = "/events/{eventId}";
    public const string ProjectEventsAround = "/projects/{projectId}/events/around";
    public const string ProjectEventsSearch = "/projects/{projectId}/events/search";
    public const string ProjectTimeline = "/projects/{projectId}/timeline";
    public const string ProjectReportPdf = "/projects/{projectId}/reports/pdf";
}
