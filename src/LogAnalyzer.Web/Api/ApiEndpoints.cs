using LogAnalyzer.Application;
using LogAnalyzer.Domain;
using LogAnalyzer.Web.Services;

namespace LogAnalyzer.Web.Api;

public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapLogAnalyzerApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapPost("/projects", async (CreateProjectRequest request, IMetadataRepository metadata, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest("Project name is required.");
            }

            return Results.Ok(await metadata.CreateProjectAsync(request.Name, request.Description, ct));
        });

        api.MapGet("/projects", async (IMetadataRepository metadata, CancellationToken ct) =>
            Results.Ok(await metadata.ListProjectsAsync(ct)));

        api.MapGet("/projects/{projectId}", async (string projectId, IMetadataRepository metadata, CancellationToken ct) =>
            await metadata.GetProjectAsync(projectId, ct) is { } project ? Results.Ok(project) : Results.NotFound());

        api.MapDelete("/projects/{projectId}", async (string projectId, IMetadataRepository metadata, CancellationToken ct) =>
        {
            await metadata.DeleteProjectAsync(projectId, ct);
            return Results.NoContent();
        });

        api.MapPost("/projects/{projectId}/uploads", async (
            string projectId,
            CreateUploadRequest request,
            IMetadataRepository metadata,
            CancellationToken ct) =>
        {
            var upload = await metadata.CreateUploadSessionAsync(
                new UploadSessionCreateRequest(projectId, request.OriginalName),
                ct);
            return Results.Ok(upload);
        });

        api.MapPost("/uploads/{uploadId}/files", async (
            string uploadId,
            HttpRequest request,
            IMetadataRepository metadata,
            ILogFileStorage storage,
            CancellationToken ct) =>
        {
            var upload = await metadata.GetUploadSessionAsync(uploadId, ct);
            if (upload is null)
            {
                return Results.NotFound();
            }

            await metadata.UpdateUploadSessionAsync(uploadId, new UploadProgressUpdate(Status: UploadStatuses.Uploading), ct);
            var form = await request.ReadFormAsync(ct);
            foreach (var file in form.Files)
            {
                await using var stream = file.OpenReadStream();
                await storage.SaveOriginalAsync(upload.ProjectId, uploadId, file.FileName, stream, ct);
            }

            await metadata.UpdateUploadSessionAsync(uploadId, new UploadProgressUpdate(Status: UploadStatuses.Uploaded), ct);
            return Results.Ok(await metadata.GetUploadSessionAsync(uploadId, ct));
        }).DisableAntiforgery();

        api.MapPost("/uploads/{uploadId}/start", async (
            string uploadId,
            StartImportRequest request,
            IMetadataRepository metadata,
            IImportJobQueue queue,
            CancellationToken ct) =>
        {
            var upload = await metadata.GetUploadSessionAsync(uploadId, ct);
            if (upload is null)
            {
                return Results.NotFound();
            }

            var options = new ImportOptions(
                string.IsNullOrWhiteSpace(request.TimeZoneId) ? "UTC" : request.TimeZoneId,
                request.IisLogsAreUtc,
                request.CombineMultilineExceptions);
            await queue.EnqueueAsync(new ImportJob(upload.ProjectId, uploadId, options), ct);
            return Results.Accepted($"/api/uploads/{uploadId}/status", upload);
        });

        api.MapGet("/uploads/{uploadId}/status", async (string uploadId, IMetadataRepository metadata, CancellationToken ct) =>
            await metadata.GetUploadSessionAsync(uploadId, ct) is { } upload ? Results.Ok(upload) : Results.NotFound());

        api.MapPost("/uploads/{uploadId}/cancel", async (
            string uploadId,
            ImportCancellationRegistry registry,
            IMetadataRepository metadata,
            CancellationToken ct) =>
        {
            registry.Cancel(uploadId);
            await metadata.UpdateUploadSessionAsync(
                uploadId,
                new UploadProgressUpdate(Status: UploadStatuses.Cancelled, FinishedAt: DateTimeOffset.UtcNow),
                ct);
            return Results.Accepted();
        });

        api.MapGet("/projects/{projectId}/logs", async (string projectId, IMetadataRepository metadata, CancellationToken ct) =>
            Results.Ok(await metadata.ListLogFilesAsync(projectId, ct)));

        api.MapGet("/logs/{logFileId}/events", async (
            string logFileId,
            int? offset,
            int? limit,
            IMetadataRepository metadata,
            ILogEventStore events,
            CancellationToken ct) =>
        {
            var logFile = await metadata.GetLogFileAsync(logFileId, ct);
            if (logFile is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(await events.SearchAsync(new LogEventSearchRequest
            {
                ProjectId = logFile.ProjectId,
                LogFileIds = [logFileId],
                Offset = offset ?? 0,
                Limit = limit ?? 500
            }, ct));
        });

        api.MapGet("/events/{eventId}", async (string eventId, ILogEventStore events, CancellationToken ct) =>
            await events.GetEventAsync(eventId, ct) is { } logEvent ? Results.Ok(logEvent) : Results.NotFound());

        api.MapGet("/projects/{projectId}/events/around", async (
            string projectId,
            HttpRequest request,
            ILogEventStore events,
            CancellationToken ct) =>
        {
            var timestamp = ReadDate(request, "timestamp") ?? DateTimeOffset.UtcNow;
            return Results.Ok(await events.SearchAsync(new LogEventSearchRequest
            {
                ProjectId = projectId,
                AroundUtc = timestamp.ToUniversalTime(),
                BeforeSeconds = ReadInt(request, "beforeSeconds", 30),
                AfterSeconds = ReadInt(request, "afterSeconds", 10),
                LogFileIds = ReadMany(request, "logFileIds"),
                Levels = ReadMany(request, "levels"),
                Query = request.Query["query"].ToString()
            }, ct));
        });

        api.MapGet("/projects/{projectId}/events/search", async (
            string projectId,
            HttpRequest request,
            ILogEventStore events,
            CancellationToken ct) =>
        {
            return Results.Ok(await events.SearchAsync(new LogEventSearchRequest
            {
                ProjectId = projectId,
                FromUtc = ReadDate(request, "from")?.ToUniversalTime(),
                ToUtc = ReadDate(request, "to")?.ToUniversalTime(),
                Query = request.Query["query"].ToString(),
                Levels = ReadMany(request, "levels"),
                LogFileIds = ReadMany(request, "logFileIds"),
                Limit = ReadInt(request, "limit", 500),
                Offset = ReadInt(request, "offset", 0)
            }, ct));
        });

        api.MapGet("/projects/{projectId}/timeline", async (
            string projectId,
            HttpRequest request,
            ILogEventStore events,
            CancellationToken ct) =>
        {
            var to = ReadDate(request, "to")?.ToUniversalTime() ?? DateTimeOffset.UtcNow;
            var from = ReadDate(request, "from")?.ToUniversalTime() ?? to.AddHours(-1);
            return Results.Ok(await events.GetTimelineAsync(new TimelineRequest
            {
                ProjectId = projectId,
                FromUtc = from,
                ToUtc = to,
                Bucket = ReadBucket(request.Query["bucket"].ToString()),
                LogFileIds = ReadMany(request, "logFileIds"),
                Levels = ReadMany(request, "levels")
            }, ct));
        });

        return app;
    }

    private static IReadOnlyCollection<string> ReadMany(HttpRequest request, string name)
    {
        return request.Query[name]
            .SelectMany(value => value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [])
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }

    private static int ReadInt(HttpRequest request, string name, int defaultValue)
    {
        return int.TryParse(request.Query[name], out var value) ? value : defaultValue;
    }

    private static DateTimeOffset? ReadDate(HttpRequest request, string name)
    {
        return DateTimeOffset.TryParse(request.Query[name], out var value) ? value : null;
    }

    private static TimeSpan ReadBucket(string? value)
    {
        return value switch
        {
            "1s" => TimeSpan.FromSeconds(1),
            "5s" => TimeSpan.FromSeconds(5),
            "10s" => TimeSpan.FromSeconds(10),
            "5m" => TimeSpan.FromMinutes(5),
            "1h" => TimeSpan.FromHours(1),
            _ => TimeSpan.FromMinutes(1)
        };
    }

    public sealed record CreateProjectRequest(string Name, string? Description);
    public sealed record CreateUploadRequest(string? OriginalName);
    public sealed record StartImportRequest(string TimeZoneId, bool IisLogsAreUtc, bool CombineMultilineExceptions);
}
