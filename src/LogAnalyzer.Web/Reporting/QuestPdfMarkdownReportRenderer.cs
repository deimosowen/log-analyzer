using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Markdown;

namespace LogAnalyzer.Web.Reporting;

public sealed class QuestPdfMarkdownReportRenderer : IReportPdfRenderer
{
    private static readonly string[] ReportFontFamilies =
    [
        "Arial",
        "DejaVu Sans",
        "Liberation Sans",
        "Noto Sans"
    ];

    private static readonly string[] MonoFontFamilies =
    [
        "Consolas",
        "DejaVu Sans Mono",
        "Liberation Mono",
        "Noto Sans Mono"
    ];

    public QuestPdfMarkdownReportRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseEnvironmentFonts = true;
    }

    public byte[] RenderMarkdown(ReportPdfRenderRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Markdown);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.MarginHorizontal(32);
                page.MarginVertical(26);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(style => style
                    .FontFamily(ReportFontFamilies)
                    .FontSize(9)
                    .FontColor("#121820"));

                page.Header().Element(content => ComposeHeader(content, request));
                page.Content()
                    .PaddingTop(16)
                    .PaddingBottom(10)
                    .DefaultTextStyle(style => style.LineHeight(1.24f))
                    .Markdown(request.Markdown);
                page.Footer().Element(ComposeFooter);
            });
        }).WithMetadata(new DocumentMetadata
        {
            Title = request.Title,
            Author = "Log Analyzer",
            Creator = "Log Analyzer",
            Subject = $"Incident report for {request.ProjectName}",
            CreationDate = request.GeneratedAtUtc.DateTime,
            ModifiedDate = request.GeneratedAtUtc.DateTime
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, ReportPdfRenderRequest request)
    {
        container
            .PaddingBottom(10)
            .BorderBottom(1)
            .BorderColor("#D8E1EA")
            .Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text(request.ProjectName)
                        .FontFamily(ReportFontFamilies)
                        .FontSize(16)
                        .Bold()
                        .FontColor("#121820");

                    column.Item().PaddingTop(3).Text(request.Title)
                        .FontFamily(ReportFontFamilies)
                        .FontSize(8)
                        .SemiBold()
                        .FontColor("#718096");
                });

                row.ConstantItem(170).AlignRight().Column(column =>
                {
                    column.Item().AlignRight().Text("Log Analyzer")
                        .FontFamily(ReportFontFamilies)
                        .FontSize(9)
                        .Bold()
                        .FontColor("#0F6B8F");

                    column.Item().AlignRight().PaddingTop(3).Text(request.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'"))
                        .FontFamily(MonoFontFamilies)
                        .FontSize(7)
                        .FontColor("#718096");
                });
            });
    }

    private static void ComposeFooter(IContainer container)
    {
        container
            .PaddingTop(8)
            .BorderTop(1)
            .BorderColor("#D8E1EA")
            .AlignCenter()
            .Text(text =>
            {
                text.DefaultTextStyle(style => style
                    .FontFamily(MonoFontFamilies)
                    .FontSize(7)
                    .FontColor("#718096"));
                text.Span("страница ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
    }
}
