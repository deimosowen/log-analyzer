using LogAnalyzer.Application;
using LogAnalyzer.Web.Api.Requests;
using LogAnalyzer.Web.Auth;
using LogAnalyzer.Web.Reporting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.Web.Api;

internal static class ReportApiEndpoints
{
    private const int MaxMarkdownCharacters = 1_000_000;
    private const long MaxRequestBytes = 5_000_000;

    public static RouteGroupBuilder MapReportEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost(ApiRoutes.ProjectReportPdf, GeneratePdfAsync)
            .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBytes));

        return api;
    }

    private static async Task<IResult> GeneratePdfAsync(
        string projectId,
        GenerateReportPdfRequest request,
        HttpContext context,
        IMetadataRepository metadata,
        IReportPdfRenderer pdfRenderer,
        IOptions<AppAuthenticationOptions> authOptions,
        CancellationToken cancellationToken)
    {
        var user = HttpCurrentUser.Get(context, authOptions);
        if (!user.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        var project = await metadata.GetProjectAsync(user.Id, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Markdown))
        {
            return Results.BadRequest("Отчет пустой.");
        }

        if (request.Markdown.Length > MaxMarkdownCharacters)
        {
            return Results.BadRequest($"Отчет слишком большой. Максимум {MaxMarkdownCharacters:N0} символов.");
        }

        var fileName = ReportFileName.NormalizePdf(request.FileName);
        var title = Path.GetFileNameWithoutExtension(fileName);
        var content = pdfRenderer.RenderMarkdown(new ReportPdfRenderRequest(
            title,
            project.Name,
            request.Markdown,
            DateTimeOffset.UtcNow));

        return Results.File(content, "application/pdf", fileName);
    }
}
