using LogAnalyzer.Domain;

namespace LogAnalyzer.Application.Analysis;

public static class IisAnalysisBuilder
{
    public static IisAnalysisResult Build(
        IEnumerable<LogEvent> events,
        int slowRequestThresholdMs,
        int topLimit)
    {
        var httpEvents = events
            .Where(static item => !string.IsNullOrWhiteSpace(item.HttpMethod) && !string.IsNullOrWhiteSpace(item.Url))
            .ToArray();

        if (httpEvents.Length == 0)
        {
            return IisAnalysisResult.Empty;
        }

        var safeTopLimit = Math.Clamp(topLimit, 1, 50);
        var safeSlowThreshold = Math.Max(0, slowRequestThresholdMs);
        var summary = new IisAnalysisSummary(
            httpEvents.Length,
            httpEvents.LongCount(static item => item.StatusCode is >= 200 and < 300),
            httpEvents.LongCount(static item => item.StatusCode is >= 300 and < 400),
            httpEvents.LongCount(static item => item.StatusCode is >= 400 and < 500),
            httpEvents.LongCount(static item => item.StatusCode >= 500),
            httpEvents.LongCount(item => item.TimeTaken >= safeSlowThreshold),
            Percentile(httpEvents.Select(static item => item.TimeTaken), 0.95),
            httpEvents.Max(static item => item.TimeTaken));

        return new IisAnalysisResult(
            summary,
            BuildEndpointAggregates(httpEvents.Where(static item => item.StatusCode >= 500), safeTopLimit),
            BuildEndpointAggregates(httpEvents.Where(static item => item.StatusCode is >= 400 and < 500), safeTopLimit),
            BuildEndpointAggregates(httpEvents.Where(item => item.TimeTaken >= safeSlowThreshold), safeTopLimit, sortByLatency: true),
            httpEvents
                .Where(item => item.TimeTaken >= safeSlowThreshold)
                .OrderByDescending(static item => item.TimeTaken)
                .ThenBy(static item => item.TimestampUtc)
                .Take(safeTopLimit)
                .Select(static item => new IisSlowRequest(
                    item.Id,
                    item.TimestampUtc,
                    item.HttpMethod,
                    NormalizeUrl(item.Url),
                    item.StatusCode,
                    item.ClientIp,
                    item.UserName,
                    item.TimeTaken,
                    item.LogFileId))
                .ToArray());
    }

    private static IReadOnlyList<IisEndpointAggregate> BuildEndpointAggregates(
        IEnumerable<LogEvent> events,
        int topLimit,
        bool sortByLatency = false)
    {
        var aggregates = events
            .GroupBy(static item => new
            {
                Method = item.HttpMethod.ToUpperInvariant(),
                Url = NormalizeUrl(item.Url),
                StatusCodeClass = item.StatusCode / 100
            })
            .Select(static group => new IisEndpointAggregate(
                group.Key.Method,
                group.Key.Url,
                group.Key.StatusCodeClass,
                group.LongCount(),
                group.LongCount(static item => item.StatusCode is >= 400 and < 500),
                group.LongCount(static item => item.StatusCode >= 500),
                Percentile(group.Select(static item => item.TimeTaken), 0.95),
                group.Max(static item => item.TimeTaken)));

        aggregates = sortByLatency
            ? aggregates.OrderByDescending(static item => item.P95TimeTaken).ThenByDescending(static item => item.Count)
            : aggregates.OrderByDescending(static item => item.Count).ThenByDescending(static item => item.MaxTimeTaken);

        return aggregates.Take(topLimit).ToArray();
    }

    private static int Percentile(IEnumerable<int> values, double percentile)
    {
        var ordered = values.Where(static value => value >= 0).Order().ToArray();
        if (ordered.Length == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(percentile * ordered.Length) - 1;
        return ordered[Math.Clamp(index, 0, ordered.Length - 1)];
    }

    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "/";
        }

        var queryIndex = url.IndexOf('?', StringComparison.Ordinal);
        return queryIndex >= 0 ? url[..queryIndex] : url;
    }
}
