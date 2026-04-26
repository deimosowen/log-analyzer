using LogAnalyzer.Application;
using LogAnalyzer.Application.Time;
using LogAnalyzer.Domain;
using LogAnalyzer.Web.Services;

namespace LogAnalyzer.Web.Api;

internal static class UploadApiEndpoints
{
    public static RouteGroupBuilder MapUploadEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost(ApiRoutes.ProjectUploads, CreateUploadAsync);
        api.MapPost(ApiRoutes.UploadFiles, UploadFilesAsync).DisableAntiforgery();
        api.MapPost(ApiRoutes.UploadStart, StartImportAsync);
        api.MapGet(ApiRoutes.UploadStatus, GetUploadStatusAsync);
        api.MapPost(ApiRoutes.UploadCancel, CancelUploadAsync);

        return api;
    }

    private static async Task<IResult> CreateUploadAsync(
        string projectId,
        CreateUploadRequest request,
        IMetadataRepository metadata,
        CancellationToken cancellationToken)
    {
        var upload = await metadata.CreateUploadSessionAsync(
            new UploadSessionCreateRequest(projectId, request.OriginalName),
            cancellationToken);

        return Results.Ok(upload);
    }

    private static async Task<IResult> UploadFilesAsync(
        string uploadId,
        HttpRequest request,
        IMetadataRepository metadata,
        ILogFileStorage storage,
        CancellationToken cancellationToken)
    {
        var upload = await metadata.GetUploadSessionAsync(uploadId, cancellationToken);
        if (upload is null)
        {
            return Results.NotFound();
        }

        await metadata.UpdateUploadSessionAsync(
            uploadId,
            new UploadProgressUpdate(Status: UploadStatuses.Uploading),
            cancellationToken);

        var form = await request.ReadFormAsync(cancellationToken);
        foreach (var file in form.Files)
        {
            await using var stream = file.OpenReadStream();
            await storage.SaveOriginalAsync(upload.ProjectId, uploadId, file.FileName, stream, cancellationToken);
        }

        await metadata.UpdateUploadSessionAsync(
            uploadId,
            new UploadProgressUpdate(Status: UploadStatuses.Uploaded),
            cancellationToken);

        return Results.Ok(await metadata.GetUploadSessionAsync(uploadId, cancellationToken));
    }

    private static async Task<IResult> StartImportAsync(
        string uploadId,
        StartImportRequest request,
        IMetadataRepository metadata,
        IImportJobQueue queue,
        CancellationToken cancellationToken)
    {
        var upload = await metadata.GetUploadSessionAsync(uploadId, cancellationToken);
        if (upload is null)
        {
            return Results.NotFound();
        }

        var options = new ImportOptions(
            string.IsNullOrWhiteSpace(request.TimeZoneId) ? TimeZoneDefaults.Utc : request.TimeZoneId,
            request.IisLogsAreUtc,
            request.CombineMultilineExceptions);

        await queue.EnqueueAsync(new ImportJob(upload.ProjectId, uploadId, options), cancellationToken);
        return Results.Accepted(ApiRoutes.UploadStatus.Replace("{uploadId}", uploadId, StringComparison.Ordinal), upload);
    }

    private static async Task<IResult> GetUploadStatusAsync(
        string uploadId,
        IMetadataRepository metadata,
        CancellationToken cancellationToken)
    {
        return await metadata.GetUploadSessionAsync(uploadId, cancellationToken) is { } upload
            ? Results.Ok(upload)
            : Results.NotFound();
    }

    private static async Task<IResult> CancelUploadAsync(
        string uploadId,
        ImportCancellationRegistry registry,
        IMetadataRepository metadata,
        CancellationToken cancellationToken)
    {
        registry.Cancel(uploadId);
        await metadata.UpdateUploadSessionAsync(
            uploadId,
            new UploadProgressUpdate(Status: UploadStatuses.Cancelled, FinishedAt: DateTimeOffset.UtcNow),
            cancellationToken);

        return Results.Accepted();
    }
}
