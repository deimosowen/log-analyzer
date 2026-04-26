using LogAnalyzer.Application.Analysis;
using LogAnalyzer.Domain;

namespace LogAnalyzer.Tests;

public sealed class CorrelationGroupingTests
{
    [Fact]
    public void GroupProblemEventsCollapsesIisQueryStringNoise()
    {
        var projectId = Guid.NewGuid().ToString("N");
        var uploadId = Guid.NewGuid().ToString("N");
        var logFileId = Guid.NewGuid().ToString("N");

        var events = new[]
        {
            CreateHttpEvent(projectId, uploadId, logFileId, "/api/items?id=1"),
            CreateHttpEvent(projectId, uploadId, logFileId, "/api/items?id=2"),
            CreateHttpEvent(projectId, uploadId, Guid.NewGuid().ToString("N"), "/api/items?id=3")
        };

        var groups = CorrelationGrouping.GroupProblemEvents(events);

        Assert.Single(groups);
        Assert.Equal(3, groups[0].Count);
        Assert.Equal(2, groups[0].LogCount);
        Assert.Equal("500 GET /api/items", groups[0].Title);
    }

    private static LogEvent CreateHttpEvent(
        string projectId,
        string uploadId,
        string logFileId,
        string url)
    {
        var timestamp = DateTimeOffset.Parse("2026-04-21T05:45:29Z");

        return new LogEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            ProjectId = projectId,
            UploadSessionId = uploadId,
            LogFileId = logFileId,
            TimestampUtc = timestamp,
            TimestampMs = timestamp.ToUnixTimeMilliseconds(),
            Level = "ERROR",
            Source = "IIS",
            LineNumber = 1,
            EndLineNumber = 1,
            Message = $"GET {url} 500",
            RawText = $"2026-04-21 05:45:29 GET {url} 500",
            HttpMethod = "GET",
            Url = url,
            StatusCode = 500
        };
    }
}
