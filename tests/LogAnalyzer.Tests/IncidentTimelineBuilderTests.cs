using LogAnalyzer.Application.Analysis;
using LogAnalyzer.Domain;

namespace LogAnalyzer.Tests;

public sealed class IncidentTimelineBuilderTests
{
    [Fact]
    public void BuildFillsEmptyBucketsAndMarksAnchor()
    {
        var from = DateTimeOffset.Parse("2026-04-21T05:45:00Z");
        var to = from.AddSeconds(30);

        var points = new[]
        {
            new TimelinePoint(from.AddSeconds(10), LogLevels.Error, 2),
            new TimelinePoint(from.AddSeconds(10), LogLevels.Warn, 1),
            new TimelinePoint(from.AddSeconds(20), LogLevels.Info, 4),
            new TimelinePoint(from.AddSeconds(20), LogLevels.Debug, 3)
        };

        var timeline = IncidentTimelineBuilder.Build(points, from, to, TimeSpan.FromSeconds(10), from.AddSeconds(15));

        Assert.Equal(4, timeline.Buckets.Count);
        Assert.Equal(10, timeline.TotalCount);
        Assert.Equal(3, timeline.ProblemCount);
        Assert.Equal(0, timeline.Buckets[0].TotalCount);
        Assert.True(timeline.Buckets[1].ContainsAnchor);
        Assert.Equal(2, timeline.Buckets[1].ErrorCount);
        Assert.Equal(1, timeline.Buckets[1].WarnCount);
        Assert.Equal(4, timeline.Buckets[2].InfoCount);
        Assert.Equal(3, timeline.Buckets[2].OtherCount);
    }

    [Theory]
    [InlineData(40, 1)]
    [InlineData(20 * 60, 60)]
    [InlineData(6 * 60 * 60, 300)]
    public void ChooseBucketKeepsTimelineCompact(int rangeSeconds, int expectedBucketSeconds)
    {
        var from = DateTimeOffset.Parse("2026-04-21T05:45:00Z");
        var to = from.AddSeconds(rangeSeconds);

        var bucket = IncidentTimelineBuilder.ChooseBucket(from, to);

        Assert.Equal(TimeSpan.FromSeconds(expectedBucketSeconds), bucket);
    }
}
