namespace LogAnalyzer.Web.Api.Requests;

public sealed record GenerateReportPdfRequest(string? FileName, string? Markdown);
