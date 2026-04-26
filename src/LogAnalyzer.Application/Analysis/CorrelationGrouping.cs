using LogAnalyzer.Domain;

namespace LogAnalyzer.Application.Analysis;

public sealed record CorrelationGroup(
    string Key,
    string Title,
    string Level,
    int Count,
    int LogCount,
    DateTimeOffset FirstTimestamp,
    DateTimeOffset LastTimestamp,
    LogEvent Representative);

public static class ProblemEventLevels
{
    public static readonly string[] Default = ["ERROR", "WARN", "FATAL"];
}

public static class CorrelationGrouping
{
    public static IReadOnlyList<CorrelationGroup> GroupProblemEvents(IEnumerable<LogEvent> events)
    {
        return events
            .GroupBy(BuildKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group.OrderBy(item => item.TimestampUtc).ToArray();
                var representative = ordered.First();

                return new CorrelationGroup(
                    group.Key,
                    BuildTitle(representative),
                    representative.Level,
                    ordered.Length,
                    ordered.Select(item => item.LogFileId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    ordered.First().TimestampUtc,
                    ordered.Last().TimestampUtc,
                    representative);
            })
            .OrderByDescending(group => SeverityRank(group.Level))
            .ThenByDescending(group => group.Count)
            .ThenBy(group => group.FirstTimestamp)
            .ToArray();
    }

    public static string BuildKey(LogEvent item)
    {
        if (IsHttpEvent(item))
        {
            return string.Join('|',
                "iis",
                item.Level,
                item.StatusCode,
                item.HttpMethod,
                NormalizeUrl(item.Url));
        }

        return string.Join('|',
            "log",
            item.Level,
            item.Source,
            NormalizeMessage(item.Message));
    }

    public static string BuildTitle(LogEvent item)
    {
        if (IsHttpEvent(item))
        {
            var method = string.IsNullOrWhiteSpace(item.HttpMethod) ? "HTTP" : item.HttpMethod;
            var url = string.IsNullOrWhiteSpace(item.Url) ? item.Message : NormalizeUrl(item.Url);
            return $"{item.StatusCode} {method} {url}";
        }

        return string.IsNullOrWhiteSpace(item.Message) ? item.RawText : item.Message;
    }

    public static string NormalizeUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var url = value.Trim();
        var queryIndex = url.IndexOf('?', StringComparison.Ordinal);
        return queryIndex >= 0 ? url[..queryIndex] : url;
    }

    private static bool IsHttpEvent(LogEvent item)
    {
        return !string.IsNullOrWhiteSpace(item.HttpMethod) || item.StatusCode > 0;
    }

    private static string NormalizeMessage(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var message = value.Trim();
        return message.Length <= 180 ? message : message[..180];
    }

    private static int SeverityRank(string level)
    {
        return level switch
        {
            "FATAL" => 3,
            "ERROR" => 2,
            "WARN" => 1,
            _ => 0
        };
    }
}
