namespace LogAnalyzer.Application;

public static class EventSearchDefaults
{
    public const int DefaultOffset = 0;
    public const int DefaultLimit = 500;
    public const int MaxLimit = 1000;
    public const int DefaultBeforeSeconds = 30;
    public const int DefaultAfterSeconds = 10;
    public const int CorrelationLimit = 1000;
    public const int RecentImportErrorsLimit = 500;

    public static readonly TimeSpan DefaultTimelineLookback = TimeSpan.FromHours(1);
    public static readonly TimeSpan DefaultTimelineBucket = TimeSpan.FromMinutes(1);
}
