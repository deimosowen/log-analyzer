namespace LogAnalyzer.Domain;

public sealed record TimelinePoint(
    DateTimeOffset BucketUtc,
    string Level,
    long Count);
