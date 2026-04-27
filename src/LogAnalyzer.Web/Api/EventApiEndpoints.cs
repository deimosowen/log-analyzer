using LogAnalyzer.Application;
using LogAnalyzer.Web.Auth;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.Web.Api;

internal static class EventApiEndpoints
{
    public static RouteGroupBuilder MapEventEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet(ApiRoutes.LogEvents, ListLogEventsAsync);
        api.MapGet(ApiRoutes.Event, GetEventAsync);
        api.MapGet(ApiRoutes.ProjectEventsAround, SearchAroundAsync);
        api.MapGet(ApiRoutes.ProjectEventsSearch, SearchProjectEventsAsync);
        api.MapGet(ApiRoutes.ProjectTimeline, GetTimelineAsync);

        return api;
    }

    private static async Task<IResult> ListLogEventsAsync(
        string logFileId,
        int? offset,
        int? limit,
        HttpContext context,
        IMetadataRepository metadata,
        ILogEventStore events,
        IOptions<AppAuthenticationOptions> authOptions,
        CancellationToken cancellationToken)
    {
        var user = HttpCurrentUser.Get(context, authOptions);
        if (!user.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        var logFile = await metadata.GetLogFileAsync(logFileId, cancellationToken);
        if (logFile is null)
        {
            return Results.NotFound();
        }

        if (await metadata.GetProjectAsync(user.Id, logFile.ProjectId, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(await events.SearchAsync(new LogEventSearchRequest
        {
            ProjectId = logFile.ProjectId,
            LogFileIds = [logFileId],
            Offset = offset ?? EventSearchDefaults.DefaultOffset,
            Limit = limit ?? EventSearchDefaults.DefaultLimit
        }, cancellationToken));
    }

    private static async Task<IResult> GetEventAsync(
        string eventId,
        HttpContext context,
        IMetadataRepository metadata,
        ILogEventStore events,
        IOptions<AppAuthenticationOptions> authOptions,
        CancellationToken cancellationToken)
    {
        var logEvent = await events.GetEventAsync(eventId, cancellationToken);
        if (logEvent is null ||
            !await CanAccessProjectAsync(logEvent.ProjectId, context, metadata, authOptions, cancellationToken))
        {
            return Results.NotFound();
        }

        return Results.Ok(logEvent);
    }

    private static async Task<IResult> SearchAroundAsync(
        string projectId,
        HttpRequest request,
        HttpContext context,
        IMetadataRepository metadata,
        ILogEventStore events,
        IOptions<AppAuthenticationOptions> authOptions,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessProjectAsync(projectId, context, metadata, authOptions, cancellationToken))
        {
            return Results.NotFound();
        }

        var timestamp = ApiQueryReader.ReadDate(request, "timestamp") ?? DateTimeOffset.UtcNow;
        return Results.Ok(await events.SearchAsync(new LogEventSearchRequest
        {
            ProjectId = projectId,
            AroundUtc = timestamp.ToUniversalTime(),
            BeforeSeconds = ApiQueryReader.ReadInt(request, "beforeSeconds", EventSearchDefaults.DefaultBeforeSeconds),
            AfterSeconds = ApiQueryReader.ReadInt(request, "afterSeconds", EventSearchDefaults.DefaultAfterSeconds),
            LogFileIds = ApiQueryReader.ReadMany(request, "logFileIds"),
            Levels = ApiQueryReader.ReadMany(request, "levels"),
            Query = request.Query["query"].ToString()
        }, cancellationToken));
    }

    private static async Task<IResult> SearchProjectEventsAsync(
        string projectId,
        HttpRequest request,
        HttpContext context,
        IMetadataRepository metadata,
        ILogEventStore events,
        IOptions<AppAuthenticationOptions> authOptions,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessProjectAsync(projectId, context, metadata, authOptions, cancellationToken))
        {
            return Results.NotFound();
        }

        return Results.Ok(await events.SearchAsync(new LogEventSearchRequest
        {
            ProjectId = projectId,
            FromUtc = ApiQueryReader.ReadDate(request, "from")?.ToUniversalTime(),
            ToUtc = ApiQueryReader.ReadDate(request, "to")?.ToUniversalTime(),
            Query = request.Query["query"].ToString(),
            Levels = ApiQueryReader.ReadMany(request, "levels"),
            LogFileIds = ApiQueryReader.ReadMany(request, "logFileIds"),
            Limit = ApiQueryReader.ReadInt(request, "limit", EventSearchDefaults.DefaultLimit),
            Offset = ApiQueryReader.ReadInt(request, "offset", EventSearchDefaults.DefaultOffset)
        }, cancellationToken));
    }

    private static async Task<IResult> GetTimelineAsync(
        string projectId,
        HttpRequest request,
        HttpContext context,
        IMetadataRepository metadata,
        ILogEventStore events,
        IOptions<AppAuthenticationOptions> authOptions,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessProjectAsync(projectId, context, metadata, authOptions, cancellationToken))
        {
            return Results.NotFound();
        }

        var to = ApiQueryReader.ReadDate(request, "to")?.ToUniversalTime() ?? DateTimeOffset.UtcNow;
        var from = ApiQueryReader.ReadDate(request, "from")?.ToUniversalTime() ??
                   to.Subtract(EventSearchDefaults.DefaultTimelineLookback);

        return Results.Ok(await events.GetTimelineAsync(new TimelineRequest
        {
            ProjectId = projectId,
            FromUtc = from,
            ToUtc = to,
            Bucket = ApiQueryReader.ReadBucket(request.Query["bucket"].ToString()),
            LogFileIds = ApiQueryReader.ReadMany(request, "logFileIds"),
            Levels = ApiQueryReader.ReadMany(request, "levels")
        }, cancellationToken));
    }

    private static async Task<bool> CanAccessProjectAsync(
        string projectId,
        HttpContext context,
        IMetadataRepository metadata,
        IOptions<AppAuthenticationOptions> authOptions,
        CancellationToken cancellationToken)
    {
        var user = HttpCurrentUser.Get(context, authOptions);
        return user.IsAuthenticated &&
               await metadata.GetProjectAsync(user.Id, projectId, cancellationToken) is not null;
    }
}
