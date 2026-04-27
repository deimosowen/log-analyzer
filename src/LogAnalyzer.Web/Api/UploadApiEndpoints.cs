using LogAnalyzer.Application;
using LogAnalyzer.Application.Time;
using LogAnalyzer.Domain;
using LogAnalyzer.Infrastructure.Storage;
using LogAnalyzer.Web.Auth;
using LogAnalyzer.Web.Services;
using Microsoft.Extensions.Options;

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
        HttpContext context,
        IMetadataRepository metadata,
        IOptions<AppAuthenticationOptions> authOptions,
        CancellationToken cancellationToken)
    {
        var user = HttpCurrentUser.Get(context, authOptions);
        if (!user.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (await metadata.GetProjectAsync(user.Id, projectId, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        var upload = await metadata.CreateUploadSessionAsync(
            new UploadSessionCreateRequest(projectId, request.OriginalName),
            cancellationToken);

        return Results.Ok(upload);
    }

    private static async Task<IResult> UploadFilesAsync(
        string uploadId,
        HttpRequest request,
        HttpContext context,
        IMetadataRepository metadata,
        ILogFileStorage storage,
        IOptions<StorageOptions> storageOptions,
        IOptions<AppAuthenticationOptions> authOptions,
        CancellationToken cancellationToken)
    {
        var user = HttpCurrentUser.Get(context, authOptions);
        if (!user.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        var upload = await metadata.GetUploadSessionAsync(uploadId, cancellationToken);
        if (upload is null)
        {
            return Results.NotFound();
        }

        if (await metadata.GetProjectAsync(user.Id, upload.ProjectId, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var maxUploadBytes = storageOptions.Value.MaxUploadBytes;
        var oversizedFile = FindOversizedFile(form.Files, maxUploadBytes);
        if (oversizedFile is not null)
        {
            await metadata.UpdateUploadSessionAsync(
                uploadId,
                new UploadProgressUpdate(Status: UploadStatuses.Failed, FinishedAt: DateTimeOffset.UtcNow),
                cancellationToken);

            return Results.Problem(
                title: "Файл превышает допустимый размер загрузки.",
                detail: $"Файл \"{oversizedFile.FileName}\" имеет размер {FormatBytes(oversizedFile.Length)}. Максимум: {FormatBytes(maxUploadBytes)}.",
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        await metadata.UpdateUploadSessionAsync(
            uploadId,
            new UploadProgressUpdate(Status: UploadStatuses.Uploading),
            cancellationToken);

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
        HttpContext context,
        IMetadataRepository metadata,
        IImportJobQueue queue,
        IOptions<AppAuthenticationOptions> authOptions,
        CancellationToken cancellationToken)
    {
        var user = HttpCurrentUser.Get(context, authOptions);
        if (!user.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        var upload = await metadata.GetUploadSessionAsync(uploadId, cancellationToken);
        if (upload is null)
        {
            return Results.NotFound();
        }

        if (await metadata.GetProjectAsync(user.Id, upload.ProjectId, cancellationToken) is null)
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
        HttpContext context,
        IMetadataRepository metadata,
        IOptions<AppAuthenticationOptions> authOptions,
        CancellationToken cancellationToken)
    {
        var user = HttpCurrentUser.Get(context, authOptions);
        if (!user.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        var upload = await metadata.GetUploadSessionAsync(uploadId, cancellationToken);
        if (upload is null || await metadata.GetProjectAsync(user.Id, upload.ProjectId, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(upload);
    }

    private static async Task<IResult> CancelUploadAsync(
        string uploadId,
        HttpContext context,
        ImportCancellationRegistry registry,
        IMetadataRepository metadata,
        IOptions<AppAuthenticationOptions> authOptions,
        CancellationToken cancellationToken)
    {
        var user = HttpCurrentUser.Get(context, authOptions);
        if (!user.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        var upload = await metadata.GetUploadSessionAsync(uploadId, cancellationToken);
        if (upload is null || await metadata.GetProjectAsync(user.Id, upload.ProjectId, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        registry.Cancel(uploadId);
        await metadata.UpdateUploadSessionAsync(
            uploadId,
            new UploadProgressUpdate(Status: UploadStatuses.Cancelled, FinishedAt: DateTimeOffset.UtcNow),
            cancellationToken);

        return Results.Accepted();
    }

    private static IFormFile? FindOversizedFile(IFormFileCollection files, long maxUploadBytes)
    {
        return files.FirstOrDefault(file => file.Length > maxUploadBytes);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }
}
