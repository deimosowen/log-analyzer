using System.IO.Compression;
using LogAnalyzer.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.Tests;

public sealed class FileSystemLogStorageTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "log-analyzer-storage-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DiscoversLogFilesInsideZipFolders()
    {
        var storage = CreateStorage();
        await using var archive = CreateZipArchiveWithEntry("logs/iis/u_ex260504.log", "#Fields: date time s-ip\n2026-05-04 09:00:00 127.0.0.1");

        await storage.SaveOriginalAsync("project", "upload", "logs.zip", archive, CancellationToken.None);

        var files = await storage.DiscoverImportFilesAsync("project", "upload", CancellationToken.None);

        var file = Assert.Single(files);
        Assert.Equal("u_ex260504.log", file.DisplayName);
        Assert.EndsWith("logs/iis/u_ex260504.log", file.OriginalPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(file.StoredPath));
    }

    [Fact]
    public async Task DiscoversLogFilesInsideSevenZipFolders()
    {
        var storage = CreateStorage();
        await using var archive = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "iis_logs.7z"));

        await storage.SaveOriginalAsync("project", "upload", "logs.7z", archive, CancellationToken.None);

        var files = await storage.DiscoverImportFilesAsync("project", "upload", CancellationToken.None);

        var file = Assert.Single(files);
        Assert.Equal("u_ex260504.log", file.DisplayName);
        Assert.EndsWith("logs/iis/u_ex260504.log", file.OriginalPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(file.StoredPath));
    }

    [Fact]
    public async Task DiscoversLogFilesInsideRarFolders()
    {
        var storage = CreateStorage();
        await using var archive = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "iis_logs.rar"));

        await storage.SaveOriginalAsync("project", "upload", "logs.rar", archive, CancellationToken.None);

        var files = await storage.DiscoverImportFilesAsync("project", "upload", CancellationToken.None);

        var file = Assert.Single(files);
        Assert.Equal("u_ex260504.log", file.DisplayName);
        Assert.EndsWith("logs/iis/u_ex260504.log", file.OriginalPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(file.StoredPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private FileSystemLogStorage CreateStorage()
    {
        return new FileSystemLogStorage(
            Options.Create(new StorageOptions { RootPath = _rootPath }),
            NullLogger<FileSystemLogStorage>.Instance);
    }

    private static MemoryStream CreateZipArchiveWithEntry(string entryName, string content)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(entryName);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }

        stream.Position = 0;
        return stream;
    }
}
