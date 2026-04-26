using System.Globalization;

namespace LogAnalyzer.Infrastructure.ClickHouse;

public static class ClickHouseSql
{
    public static string QuoteLiteral(string value)
    {
        return "'" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal) + "'";
    }

    public static string ToUuid(string id)
    {
        return Guid.TryParse(id, out var guid)
            ? guid.ToString()
            : Guid.ParseExact(id, "N").ToString();
    }

    public static string ToDateTime64(DateTimeOffset value)
    {
        return value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.ffff", CultureInfo.InvariantCulture);
    }

    public static string Identifier(string value)
    {
        return string.Concat(value.Where(static ch => char.IsLetterOrDigit(ch) || ch == '_')) is { Length: > 0 } identifier
            ? identifier
            : "default";
    }
}
