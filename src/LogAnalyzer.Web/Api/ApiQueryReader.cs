using LogAnalyzer.Application;

namespace LogAnalyzer.Web.Api;

internal static class ApiQueryReader
{
    public static IReadOnlyCollection<string> ReadMany(HttpRequest request, string name)
    {
        return request.Query[name]
            .SelectMany(value => value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [])
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }

    public static int ReadInt(HttpRequest request, string name, int defaultValue)
    {
        return int.TryParse(request.Query[name], out var value) ? value : defaultValue;
    }

    public static DateTimeOffset? ReadDate(HttpRequest request, string name)
    {
        return DateTimeOffset.TryParse(request.Query[name], out var value) ? value : null;
    }

    public static TimeSpan ReadBucket(string? value)
    {
        return value switch
        {
            "1s" => TimeSpan.FromSeconds(1),
            "5s" => TimeSpan.FromSeconds(5),
            "10s" => TimeSpan.FromSeconds(10),
            "5m" => TimeSpan.FromMinutes(5),
            "1h" => TimeSpan.FromHours(1),
            _ => EventSearchDefaults.DefaultTimelineBucket
        };
    }
}
