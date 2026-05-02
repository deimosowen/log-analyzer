namespace LogAnalyzer.Web.Api;

public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapLogAnalyzerApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup(ApiRoutes.Root);

        api.MapProjectEndpoints();
        api.MapUploadEndpoints();
        api.MapEventEndpoints();
        api.MapReportEndpoints();

        return app;
    }
}
