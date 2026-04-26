namespace LogAnalyzer.Domain;

public static class LogLevels
{
    public const string Fatal = "FATAL";
    public const string Error = "ERROR";
    public const string Warn = "WARN";
    public const string Info = "INFO";
    public const string Debug = "DEBUG";
    public const string Trace = "TRACE";

    public static readonly string[] Known =
    [
        Error,
        Warn,
        Info,
        Debug,
        Trace,
        Fatal
    ];

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToUpperInvariant();
        return normalized switch
        {
            "WARNING" => Warn,
            "ERR" => Error,
            _ => normalized
        };
    }
}
