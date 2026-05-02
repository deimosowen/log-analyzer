namespace LogAnalyzer.Web.Reporting;

public interface IReportPdfRenderer
{
    byte[] RenderMarkdown(ReportPdfRenderRequest request);
}
