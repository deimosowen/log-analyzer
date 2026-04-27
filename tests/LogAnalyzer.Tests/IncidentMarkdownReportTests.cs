using LogAnalyzer.Application.Analysis;
using LogAnalyzer.Application.Reporting;
using LogAnalyzer.Domain;

namespace LogAnalyzer.Tests;

public sealed class IncidentMarkdownReportTests
{
    [Fact]
    public void BuildsIncidentReportWithSelectedEventGroupsAndRows()
    {
        var selected = CreateEvent("selected", "main.log", "ERROR", "Payment failed | retry", 0);
        var related = CreateEvent("related", "worker.log", "WARN", "Queue is slow", 5);
        var groups = new[]
        {
            new CorrelationGroup(
                "group-1",
                "Payment failed | retry",
                "ERROR",
                2,
                2,
                selected.TimestampUtc,
                related.TimestampUtc,
                selected)
        };

        var report = IncidentMarkdownReportBuilder.Build(new IncidentMarkdownReportRequest(
            "Production Incident",
            "UTC",
            "main.log, worker.log",
            "payment",
            30,
            10,
            selected,
            [selected, related],
            groups,
            2,
            static timestamp => timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
            item => item.Id == "selected" ? "0s" : "+5s",
            _ => "0s..+5s",
            id => id));

        Assert.EndsWith("-report.md", report.FileName);
        Assert.Contains("# Отчет анализа инцидента", report.Content);
        Assert.Contains("Payment failed \\| retry", report.Content);
        Assert.Contains("| +5s | 2026-04-21 05:45:34 | WARN | worker.log |", report.Content);
        Assert.Contains("```text", report.Content);
    }

    private static LogEvent CreateEvent(string id, string logFileId, string level, string message, int secondsOffset)
    {
        var timestamp = new DateTimeOffset(2026, 4, 21, 5, 45, 29, TimeSpan.Zero)
            .AddSeconds(secondsOffset);

        return new LogEvent
        {
            Id = id,
            ProjectId = "project",
            UploadSessionId = "upload",
            LogFileId = logFileId,
            TimestampUtc = timestamp,
            TimestampMs = timestamp.ToUnixTimeMilliseconds(),
            Level = level,
            Source = "Demo.Service",
            ThreadId = "42",
            LineNumber = 100 + secondsOffset,
            EndLineNumber = 100 + secondsOffset,
            ByteOffset = 0,
            Message = message,
            RawText = $"{level} {message}",
            Exception = string.Empty
        };
    }
}
