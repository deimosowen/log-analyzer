using LogAnalyzer.Application;
using LogAnalyzer.Domain;
using LogAnalyzer.Web.Auth;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.Web.Api;

internal static class ProjectApiEndpoints
{
    public static RouteGroupBuilder MapProjectEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost(ApiRoutes.Projects, CreateProjectAsync);
        api.MapGet(ApiRoutes.Projects, ListProjectsAsync);
        api.MapGet(ApiRoutes.Project, GetProjectAsync);
        api.MapDelete(ApiRoutes.Project, DeleteProjectAsync);
        api.MapGet(ApiRoutes.ProjectLogs, ListProjectLogsAsync);

        return api;
    }

    private static async Task<IResult> CreateProjectAsync(
        CreateProjectRequest request,
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

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest("Укажите название проекта.");
        }

        var project = await metadata.CreateProjectAsync(user.Id, request.Name, request.Description, cancellationToken);
        return Results.Ok(project);
    }

    private static async Task<IResult> ListProjectsAsync(
        HttpContext context,
        IMetadataRepository metadata,
        IOptions<AppAuthenticationOptions> authOptions,
        CancellationToken cancellationToken)
    {
        var user = HttpCurrentUser.Get(context, authOptions);
        return user.IsAuthenticated
            ? Results.Ok(await metadata.ListProjectsAsync(user.Id, cancellationToken))
            : Results.Unauthorized();
    }

    private static async Task<IResult> GetProjectAsync(
        string projectId,
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

        return await metadata.GetProjectAsync(user.Id, projectId, cancellationToken) is { } project
            ? Results.Ok(project)
            : Results.NotFound();
    }

    private static async Task<IResult> DeleteProjectAsync(
        string projectId,
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

        await metadata.DeleteProjectAsync(user.Id, projectId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListProjectLogsAsync(
        string projectId,
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

        return Results.Ok(await metadata.ListLogFilesAsync(projectId, cancellationToken));
    }
}
