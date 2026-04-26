namespace LogAnalyzer.Infrastructure.Storage;

public static class StorageDefaults
{
    public const string RootPath = "storage";
    public const long MaxUploadBytes = 1024L * 1024L * 1024L;
    public const long MaxExtractedBytes = 5L * 1024L * 1024L * 1024L;
    public const int MaxArchiveFiles = 10000;
    public const int MaxArchiveDepth = 32;
    public const string OriginalDirectoryName = "original";
    public const string ExtractedDirectoryName = "extracted";
}
