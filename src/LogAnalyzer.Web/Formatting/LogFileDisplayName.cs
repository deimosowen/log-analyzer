using LogAnalyzer.Domain;

namespace LogAnalyzer.Web.Formatting;

public static class LogFileDisplayName
{
    private static readonly string[] TechnicalPrefixes = ["original/", "extracted/"];

    public static string Format(LogFileEntity log)
    {
        return Format(log.DisplayName);
    }

    public static string Format(string value)
    {
        var normalized = value.Replace('\\', '/');
        foreach (var prefix in TechnicalPrefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[prefix.Length..];
                break;
            }
        }

        var fileName = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? normalized;
        var underscoreIndex = fileName.IndexOf('_', StringComparison.Ordinal);
        if (underscoreIndex == 32 && fileName[..underscoreIndex].All(IsHex))
        {
            fileName = fileName[(underscoreIndex + 1)..];
        }

        return string.IsNullOrWhiteSpace(fileName) ? value : fileName;
    }

    private static bool IsHex(char value)
    {
        return value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
    }
}
