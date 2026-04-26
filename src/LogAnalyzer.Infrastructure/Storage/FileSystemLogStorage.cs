using System.IO.Compression;
using System.Security.Cryptography;
using LogAnalyzer.Application;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.Infrastructure.Storage;

public sealed class FileSystemLogStorage : ILogFileStorage
{
    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip"
    };

    private static readonly HashSet<string> SupportedLogExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "",
        ".log",
        ".txt",
        ".csv",
        ".trace",
        ".iis",
        ".w3c"
    };

    private readonly StorageOptions _options;
    private readonly ILogger<FileSystemLogStorage> _logger;

    public FileSystemLogStorage(IOptions<StorageOptions> options, ILogger<FileSystemLogStorage> logger)
    {
        _options = options.Value;
        RootPath = Path.GetFullPath(_options.RootPath);
        Directory.CreateDirectory(RootPath);
        _logger = logger;
    }

    public string RootPath { get; }

    public async Task<StoredUploadFile> SaveOriginalAsync(
        string projectId,
        string uploadSessionId,
        string originalName,
        Stream content,
        CancellationToken cancellationToken)
    {
        var safeName = MakeSafeFileName(originalName);
        var originalDirectory = Path.Combine(RootPath, projectId, uploadSessionId, StorageDefaults.OriginalDirectoryName);
        Directory.CreateDirectory(originalDirectory);

        var storedPath = Path.Combine(originalDirectory, $"{Guid.NewGuid():N}_{safeName}");
        await using var fileStream = File.Create(storedPath);
        await content.CopyToAsync(fileStream, cancellationToken);
        return new StoredUploadFile(originalName, storedPath, fileStream.Length);
    }

    public async Task<IReadOnlyList<ImportFileCandidate>> DiscoverImportFilesAsync(
        string projectId,
        string uploadSessionId,
        CancellationToken cancellationToken)
    {
        var uploadRoot = Path.Combine(RootPath, projectId, uploadSessionId);
        var originalRoot = Path.Combine(uploadRoot, StorageDefaults.OriginalDirectoryName);
        var extractedRoot = Path.Combine(uploadRoot, StorageDefaults.ExtractedDirectoryName);
        Directory.CreateDirectory(extractedRoot);

        if (!Directory.Exists(originalRoot))
        {
            return [];
        }

        foreach (var archive in Directory.EnumerateFiles(originalRoot, "*", SearchOption.AllDirectories)
                     .Where(path => ArchiveExtensions.Contains(Path.GetExtension(path))))
        {
            var target = Path.Combine(extractedRoot, Path.GetFileNameWithoutExtension(archive));
            Directory.CreateDirectory(target);
            await ExtractZipSafelyAsync(archive, target, cancellationToken);
        }

        var files = Directory.EnumerateFiles(originalRoot, "*", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(extractedRoot, "*", SearchOption.AllDirectories))
            .Where(IsSupportedLogFile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var result = new List<ImportFileCandidate>(files.Length);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(file);
            var originalPath = GetDisplayPath(uploadRoot, file);
            result.Add(new ImportFileCandidate(
                originalPath,
                file,
                GetFriendlyDisplayName(originalPath),
                info.Length,
                await ComputeHashAsync(file, cancellationToken)));
        }

        return result;
    }

    private async Task ExtractZipSafelyAsync(string archivePath, string targetDirectory, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Extracting ZIP archive {Archive}", archivePath);
        var targetRoot = Path.GetFullPath(targetDirectory);
        var fileCount = 0;
        var extractedBytes = 0L;

        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                continue;
            }

            fileCount++;
            if (fileCount > _options.MaxArchiveFiles)
            {
                throw new InvalidOperationException($"Archive contains more than {_options.MaxArchiveFiles} files.");
            }

            var depth = entry.FullName.Count(ch => ch is '/' or '\\');
            if (depth > _options.MaxArchiveDepth)
            {
                throw new InvalidOperationException($"Archive nesting depth is greater than {_options.MaxArchiveDepth}.");
            }

            extractedBytes += entry.Length;
            if (extractedBytes > _options.MaxExtractedBytes)
            {
                throw new InvalidOperationException($"Extracted data is larger than {_options.MaxExtractedBytes} bytes.");
            }

            var destinationPath = Path.GetFullPath(Path.Combine(
                targetRoot,
                entry.FullName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));

            if (!destinationPath.StartsWith(targetRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Archive entry tries to write outside extraction directory.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await using var entryStream = entry.Open();
            await using var output = File.Create(destinationPath);
            await entryStream.CopyToAsync(output, cancellationToken);
        }
    }

    private static bool IsSupportedLogFile(string path)
    {
        return !ArchiveExtensions.Contains(Path.GetExtension(path)) &&
               SupportedLogExtensions.Contains(Path.GetExtension(path));
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GetDisplayPath(string root, string path)
    {
        return Path.GetRelativePath(root, path).Replace('\\', '/');
    }

    private static string GetFriendlyDisplayName(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        foreach (var prefix in new[]
        {
            StorageDefaults.OriginalDirectoryName + "/",
            StorageDefaults.ExtractedDirectoryName + "/"
        })
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

        return string.IsNullOrWhiteSpace(fileName) ? relativePath : fileName;
    }

    private static bool IsHex(char value)
    {
        return value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
    }

    private static string MakeSafeFileName(string originalName)
    {
        var fileName = Path.GetFileName(originalName);
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(fileName) ? "upload.bin" : fileName;
    }
}
