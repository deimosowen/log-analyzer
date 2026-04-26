using LogAnalyzer.Application;
using LogAnalyzer.Domain;

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
        IMetadataRepository metadata,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest("Project name is required.");
        }

        var project = await metadata.CreateProjectAsync(request.Name, request.Description, cancellationToken);
        return Results.Ok(project);
    }

    private static async Task<IResult> ListProjectsAsync(
        IMetadataRepository metadata,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await metadata.ListProjectsAsync(cancellationToken));
    }

    private static async Task<IResult> GetProjectAsync(
        string projectId,
        IMetadataRepository metadata,
        CancellationToken cancellationToken)
    {
        return await metadata.GetProjectAsync(projectId, cancellationToken) is { } project
            ? Results.Ok(project)
            : Results.NotFound();
    }

    private static async Task<IResult> DeleteProjectAsync(
        string projectId,
        IMetadataRepository metadata,
        CancellationToken cancellationToken)
    {
        await metadata.DeleteProjectAsync(projectId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListProjectLogsAsync(
        string projectId,
        IMetadataRepository metadata,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await metadata.ListLogFilesAsync(projectId, cancellationToken));
    }
}
