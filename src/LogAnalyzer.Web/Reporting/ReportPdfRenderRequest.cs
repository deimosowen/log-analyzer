namespace LogAnalyzer.Web.Reporting;

public sealed record ReportPdfRenderRequest(
    string Title,
    string ProjectName,
    string Markdown,
    DateTimeOffset GeneratedAtUtc);
