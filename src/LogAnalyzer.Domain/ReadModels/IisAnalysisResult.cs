namespace LogAnalyzer.Domain;

public sealed record IisAnalysisResult(
    IisAnalysisSummary Summary,
    IReadOnlyList<IisEndpointAggregate> ServerErrorEndpoints,
    IReadOnlyList<IisEndpointAggregate> ClientErrorEndpoints,
    IReadOnlyList<IisEndpointAggregate> SlowEndpoints,
    IReadOnlyList<IisSlowRequest> SlowRequests)
{
    public static IisAnalysisResult Empty { get; } = new(
        IisAnalysisSummary.Empty,
        [],
        [],
        [],
        []);
}

public sealed record IisAnalysisSummary(
    long TotalRequests,
    long SuccessCount,
    long RedirectCount,
    long ClientErrorCount,
    long ServerErrorCount,
    long SlowRequestCount,
    int P95TimeTaken,
    int MaxTimeTaken)
{
    public static IisAnalysisSummary Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0);
}

public sealed record IisEndpointAggregate(
    string Method,
    string Url,
    int StatusCodeClass,
    long Count,
    long ClientErrorCount,
    long ServerErrorCount,
    int P95TimeTaken,
    int MaxTimeTaken);

public sealed record IisSlowRequest(
    string EventId,
    DateTimeOffset TimestampUtc,
    string Method,
    string Url,
    int StatusCode,
    string ClientIp,
    string UserName,
    int TimeTaken,
    string LogFileId);
