using System.Globalization;

namespace LogAnalyzer.Application.Time;

public static class TimeZoneHelper
{
    private static readonly Dictionary<string, string> WindowsFallbacks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Europe/Moscow"] = "Russian Standard Time",
        ["UTC"] = "UTC",
        ["Etc/UTC"] = "UTC"
    };

    public static TimeZoneInfo Find(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException) when (WindowsFallbacks.TryGetValue(timeZoneId, out var windowsId))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        }
        catch (InvalidTimeZoneException) when (WindowsFallbacks.TryGetValue(timeZoneId, out var windowsId))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        }
    }

    public static DateTimeOffset ConvertLocalToUtc(DateTime localTime, string timeZoneId)
    {
        var timeZone = Find(timeZoneId);
        var unspecified = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, timeZone), TimeSpan.Zero);
    }

    public static DateTimeOffset ConvertIisTime(DateTime dateTime, LogParserContext context)
    {
        if (context.IisLogsAreUtc)
        {
            return new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));
        }

        return ConvertLocalToUtc(dateTime, context.TimeZoneId);
    }

    public static bool TryParseLogTimestamp(string value, out DateTime timestamp)
    {
        var formats = new[]
        {
            "yyyy-MM-dd HH:mm:ss.FFFFFFF",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy/MM/dd HH:mm:ss.FFFFFFF",
            "yyyy/MM/dd HH:mm:ss",
            "dd.MM.yyyy HH:mm:ss.FFFFFFF",
            "dd.MM.yyyy HH:mm:ss"
        };

        return DateTime.TryParseExact(
            value,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out timestamp);
    }

    public static long ToUnixMilliseconds(DateTimeOffset timestampUtc)
    {
        return timestampUtc.ToUnixTimeMilliseconds();
    }
}
