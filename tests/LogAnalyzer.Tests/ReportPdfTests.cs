using LogAnalyzer.Web.Reporting;

namespace LogAnalyzer.Tests;

public sealed class ReportPdfTests
{
    [Fact]
    public void NormalizePdfFileNameKeepsBaseNameAndPdfExtension()
    {
        var fileName = ReportFileName.NormalizePdf(@"..\incident-report.md");

        Assert.Equal("incident-report.pdf", fileName);
    }

    [Fact]
    public void QuestPdfRendererBuildsPdfFromMarkdown()
    {
        var renderer = new QuestPdfMarkdownReportRenderer();

        var content = renderer.RenderMarkdown(new ReportPdfRenderRequest(
            "incident-report",
            "Production",
            """
            # Отчет

            | Время | Уровень | Сообщение |
            | --- | --- | --- |
            | 2026-05-02 12:00:00 | ERROR | Ошибка обработки |
            """,
            DateTimeOffset.UtcNow));

        Assert.Equal("%PDF"u8.ToArray(), content[..4]);
        Assert.True(content.Length > 1024);
    }
}
