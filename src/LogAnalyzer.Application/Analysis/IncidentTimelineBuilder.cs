using LogAnalyzer.Domain;

namespace LogAnalyzer.Application.Analysis;

public static class IncidentTimelineBuilder
{
    private const int PreferredMaxBucketCount = 96;

    private static readonly TimeSpan[] BucketSteps =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5)
    ];

    private static readonly HashSet<string> NamedLevelBuckets = new(StringComparer.OrdinalIgnoreCase)
    {
        LogLevels.Fatal,
        LogLevels.Error,
        LogLevels.Warn,
        LogLevels.Info
    };

    public static TimeSpan ChooseBucket(DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        var range = toUtc - fromUtc;
        if (range <= TimeSpan.Zero)
        {
            return BucketSteps[0];
        }

        foreach (var bucket in BucketSteps)
        {
            if (range.TotalMilliseconds / bucket.TotalMilliseconds <= PreferredMaxBucketCount)
            {
                return bucket;
            }
        }

        return BucketSteps[^1];
    }

    public static IncidentTimeline Build(
        IEnumerable<TimelinePoint> points,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        TimeSpan bucket,
        DateTimeOffset? anchorUtc)
    {
        var safeBucket = bucket <= TimeSpan.Zero ? BucketSteps[0] : bucket;
        var alignedFrom = FloorToBucket(fromUtc.ToUniversalTime(), safeBucket);
        var alignedTo = FloorToBucket(toUtc.ToUniversalTime(), safeBucket);
        if (alignedTo < alignedFrom)
        {
            (alignedFrom, alignedTo) = (alignedTo, alignedFrom);
        }

        var grouped = points
            .GroupBy(point => FloorToBucket(point.BucketUtc.ToUniversalTime(), safeBucket))
            .ToDictionary(
                group => group.Key,
                group => group
                    .GroupBy(point => LogLevels.Normalize(point.Level), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(levelGroup => levelGroup.Key, levelGroup => levelGroup.Sum(point => point.Count), StringComparer.OrdinalIgnoreCase));

        var buckets = new List<IncidentTimelineBucket>();
        for (var cursor = alignedFrom; cursor <= alignedTo; cursor = cursor.Add(safeBucket))
        {
            grouped.TryGetValue(cursor, out var counts);
            var bucketEnd = cursor.Add(safeBucket);
            buckets.Add(new IncidentTimelineBucket(
                cursor,
                bucketEnd,
                Count(counts, LogLevels.Fatal),
                Count(counts, LogLevels.Error),
                Count(counts, LogLevels.Warn),
                Count(counts, LogLevels.Info),
                CountOther(counts),
                ContainsAnchor(cursor, bucketEnd, anchorUtc?.ToUniversalTime())));
        }

        return new IncidentTimeline(fromUtc.ToUniversalTime(), toUtc.ToUniversalTime(), safeBucket, buckets);
    }

    private static long Count(IReadOnlyDictionary<string, long>? counts, string level)
    {
        return counts is not null && counts.TryGetValue(level, out var count) ? count : 0;
    }

    private static long CountOther(IReadOnlyDictionary<string, long>? counts)
    {
        if (counts is null)
        {
            return 0;
        }

        return counts
            .Where(static item => !NamedLevelBuckets.Contains(item.Key))
            .Sum(static item => item.Value);
    }

    private static bool ContainsAnchor(DateTimeOffset bucketStart, DateTimeOffset bucketEnd, DateTimeOffset? anchorUtc)
    {
        return anchorUtc is not null && anchorUtc >= bucketStart && anchorUtc < bucketEnd;
    }

    private static DateTimeOffset FloorToBucket(DateTimeOffset value, TimeSpan bucket)
    {
        var bucketMs = Math.Max(1000L, (long)bucket.TotalMilliseconds);
        var timestampMs = value.ToUnixTimeMilliseconds();
        return DateTimeOffset.FromUnixTimeMilliseconds(timestampMs / bucketMs * bucketMs);
    }
}
