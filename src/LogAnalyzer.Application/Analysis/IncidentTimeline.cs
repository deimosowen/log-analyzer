using LogAnalyzer.Domain;

namespace LogAnalyzer.Application.Analysis;

public sealed record IncidentTimeline(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    TimeSpan Bucket,
    IReadOnlyList<IncidentTimelineBucket> Buckets)
{
    public static IncidentTimeline Empty { get; } = new(
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch,
        TimeSpan.FromSeconds(1),
        []);

    public bool IsEmpty => Buckets.Count == 0 || TotalCount == 0;
    public long TotalCount => Buckets.Sum(static bucket => bucket.TotalCount);
    public long ProblemCount => Buckets.Sum(static bucket => bucket.ProblemCount);
    public long MaxBucketCount => Buckets.Count == 0 ? 0 : Buckets.Max(static bucket => bucket.TotalCount);
}

public sealed record IncidentTimelineBucket(
    DateTimeOffset BucketUtc,
    DateTimeOffset BucketEndUtc,
    long FatalCount,
    long ErrorCount,
    long WarnCount,
    long InfoCount,
    long OtherCount,
    bool ContainsAnchor)
{
    public long TotalCount => FatalCount + ErrorCount + WarnCount + InfoCount + OtherCount;
    public long ProblemCount => FatalCount + ErrorCount + WarnCount;

    public string DominantLevel
    {
        get
        {
            var levels = new[]
            {
                (Level: LogLevels.Fatal, Count: FatalCount),
                (Level: LogLevels.Error, Count: ErrorCount),
                (Level: LogLevels.Warn, Count: WarnCount),
                (Level: LogLevels.Info, Count: InfoCount),
                (Level: string.Empty, Count: OtherCount)
            };

            return levels
                .OrderByDescending(static item => item.Count)
                .ThenByDescending(static item => SeverityRank(item.Level))
                .FirstOrDefault(static item => item.Count > 0)
                .Level;
        }
    }

    private static int SeverityRank(string level)
    {
        return level switch
        {
            LogLevels.Fatal => 4,
            LogLevels.Error => 3,
            LogLevels.Warn => 2,
            LogLevels.Info => 1,
            _ => 0
        };
    }
}
