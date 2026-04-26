namespace LogAnalyzer.Infrastructure.Storage;

public sealed class StorageOptions
{
    public string RootPath { get; set; } = StorageDefaults.RootPath;
    public long MaxUploadBytes { get; set; } = StorageDefaults.MaxUploadBytes;
    public long MaxExtractedBytes { get; set; } = StorageDefaults.MaxExtractedBytes;
    public int MaxArchiveFiles { get; set; } = StorageDefaults.MaxArchiveFiles;
    public int MaxArchiveDepth { get; set; } = StorageDefaults.MaxArchiveDepth;
}
