using System.IO.Compression;
using LogAnalyzer.Application;
using LogAnalyzer.Application.Parsing;
using LogAnalyzer.Application.Projects;
using LogAnalyzer.Domain;
using LogAnalyzer.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.Tests;

public sealed class ImportProcessorTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "log-analyzer-import-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ImportsLogFromFolderInsideZipArchive()
    {
        var storage = CreateStorage();
        await using var archive = CreateZipArchiveWithEntry(
            "logs/app/nested.log",
            "2026-05-04 09:00:00 ERROR Nested archive log");

        await storage.SaveOriginalAsync("project", "upload", "logs.zip", archive, CancellationToken.None);

        var metadata = new RecordingMetadataRepository();
        var eventStore = new RecordingLogEventStore();
        var processor = new ImportProcessor(
            metadata,
            eventStore,
            storage,
            new LogParserSelector([new SingleEventParser()]),
            NullLogger<ImportProcessor>.Instance);

        await processor.ProcessAsync(
            new ImportJob("project", "upload", new ImportOptions("UTC", IisLogsAreUtc: true, CombineMultilineExceptions: true)),
            CancellationToken.None);

        var logFile = Assert.Single(metadata.LogFiles);
        var logEvent = Assert.Single(eventStore.InsertedEvents);
        Assert.Equal("nested.log", logFile.DisplayName);
        Assert.EndsWith("logs/app/nested.log", logFile.OriginalPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(logFile.Id, logEvent.LogFileId);
        Assert.Equal("Nested archive log", logEvent.Message);
        Assert.Contains(metadata.LogFileUpdates, update => update.Status == LogFileStatuses.Completed && update.LineCount == 1);
        Assert.Contains(metadata.Updates, update => update.ProcessedFiles == 1 && update.TotalLines == 1);
        Assert.Contains(metadata.Updates, update => update.Status == UploadStatuses.Completed);
        Assert.Empty(metadata.Errors);
    }

    // Fixtures/nested_logs.7z / nested_logs.rar: при изменении nested.log пересобрать через «7z a -t7z …» или WinRAR «rar a -r …» (см. каталог Fixtures).
    [Fact]
    public async Task ImportsLogFromFolderInsideSevenZipArchive()
    {
        var storage = CreateStorage();
        await using var archive = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "nested_logs.7z"));

        await storage.SaveOriginalAsync("project", "upload", "logs.7z", archive, CancellationToken.None);

        var metadata = new RecordingMetadataRepository();
        var eventStore = new RecordingLogEventStore();
        var processor = new ImportProcessor(
            metadata,
            eventStore,
            storage,
            new LogParserSelector([new SingleEventParser()]),
            NullLogger<ImportProcessor>.Instance);

        await processor.ProcessAsync(
            new ImportJob("project", "upload", new ImportOptions("UTC", IisLogsAreUtc: true, CombineMultilineExceptions: true)),
            CancellationToken.None);

        var logFile = Assert.Single(metadata.LogFiles);
        var logEvent = Assert.Single(eventStore.InsertedEvents);
        Assert.Equal("nested.log", logFile.DisplayName);
        Assert.EndsWith("logs/app/nested.log", logFile.OriginalPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(logFile.Id, logEvent.LogFileId);
        Assert.Equal("Nested archive log", logEvent.Message);
        Assert.Contains(metadata.LogFileUpdates, update => update.Status == LogFileStatuses.Completed && update.LineCount == 1);
        Assert.Contains(metadata.Updates, update => update.ProcessedFiles == 1 && update.TotalLines == 1);
        Assert.Contains(metadata.Updates, update => update.Status == UploadStatuses.Completed);
        Assert.Empty(metadata.Errors);
    }

    [Fact]
    public async Task ImportsLogFromFolderInsideRarArchive()
    {
        var storage = CreateStorage();
        await using var archive = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "nested_logs.rar"));

        await storage.SaveOriginalAsync("project", "upload", "logs.rar", archive, CancellationToken.None);

        var metadata = new RecordingMetadataRepository();
        var eventStore = new RecordingLogEventStore();
        var processor = new ImportProcessor(
            metadata,
            eventStore,
            storage,
            new LogParserSelector([new SingleEventParser()]),
            NullLogger<ImportProcessor>.Instance);

        await processor.ProcessAsync(
            new ImportJob("project", "upload", new ImportOptions("UTC", IisLogsAreUtc: true, CombineMultilineExceptions: true)),
            CancellationToken.None);

        var logFile = Assert.Single(metadata.LogFiles);
        var logEvent = Assert.Single(eventStore.InsertedEvents);
        Assert.Equal("nested.log", logFile.DisplayName);
        Assert.EndsWith("logs/app/nested.log", logFile.OriginalPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(logFile.Id, logEvent.LogFileId);
        Assert.Equal("Nested archive log", logEvent.Message);
        Assert.Contains(metadata.LogFileUpdates, update => update.Status == LogFileStatuses.Completed && update.LineCount == 1);
        Assert.Contains(metadata.Updates, update => update.ProcessedFiles == 1 && update.TotalLines == 1);
        Assert.Contains(metadata.Updates, update => update.Status == UploadStatuses.Completed);
        Assert.Empty(metadata.Errors);
    }

    [Fact]
    public async Task FailsImportWhenStorageHasNoSupportedFiles()
    {
        var metadata = new RecordingMetadataRepository();
        var processor = new ImportProcessor(
            metadata,
            new NotUsedLogEventStore(),
            new EmptyLogFileStorage(),
            new LogParserSelector([]),
            NullLogger<ImportProcessor>.Instance);

        await processor.ProcessAsync(
            new ImportJob("project", "upload", new ImportOptions("UTC", IisLogsAreUtc: true, CombineMultilineExceptions: true)),
            CancellationToken.None);

        Assert.Contains(metadata.Updates, update => update.Status == UploadStatuses.Failed && update.ErrorCount == 1);
        var error = Assert.Single(metadata.Errors);
        Assert.Contains("Не найдено файлов логов", error.ErrorMessage, StringComparison.Ordinal);
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

    private sealed class SingleEventParser : ILogParser
    {
        public string Name => "Single event test parser";
        public string Format => LogFormats.NLogPlain;

        public bool CanParse(LogSample sample)
        {
            return sample.FileName.Equals("nested.log", StringComparison.OrdinalIgnoreCase);
        }

        public async IAsyncEnumerable<LogEvent> ParseAsync(
            Stream stream,
            LogParserContext context,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(stream);
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                yield break;
            }

            var timestamp = new DateTimeOffset(2026, 5, 4, 9, 0, 0, TimeSpan.Zero);
            yield return new LogEvent
            {
                Id = "event-1",
                ProjectId = context.ProjectId,
                UploadSessionId = context.UploadSessionId,
                LogFileId = context.LogFileId,
                TimestampUtc = timestamp,
                TimestampMs = timestamp.ToUnixTimeMilliseconds(),
                Level = LogLevels.Error,
                LineNumber = 1,
                EndLineNumber = 1,
                Message = "Nested archive log",
                RawText = line
            };
        }
    }

    private sealed class EmptyLogFileStorage : ILogFileStorage
    {
        public string RootPath => Path.GetTempPath();

        public Task<StoredUploadFile> SaveOriginalAsync(
            string projectId,
            string uploadSessionId,
            string originalName,
            Stream content,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ImportFileCandidate>> DiscoverImportFilesAsync(
            string projectId,
            string uploadSessionId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ImportFileCandidate>>([]);
        }
    }

    private sealed class RecordingMetadataRepository : IMetadataRepository
    {
        public List<UploadProgressUpdate> Updates { get; } = [];
        public List<ImportErrorEntity> Errors { get; } = [];
        public List<LogFileEntity> LogFiles { get; } = [];
        public List<LogFileUpdate> LogFileUpdates { get; } = [];

        public Task UpdateUploadSessionAsync(string uploadSessionId, UploadProgressUpdate update, CancellationToken cancellationToken)
        {
            Updates.Add(update);
            return Task.CompletedTask;
        }

        public Task AddImportErrorAsync(ImportErrorEntity error, CancellationToken cancellationToken)
        {
            Errors.Add(error);
            return Task.CompletedTask;
        }

        public Task<LogFileEntity> CreateLogFileAsync(LogFileEntity logFile, CancellationToken cancellationToken)
        {
            LogFiles.Add(logFile);
            return Task.FromResult(logFile);
        }

        public Task UpdateLogFileAsync(string logFileId, LogFileUpdate update, CancellationToken cancellationToken)
        {
            LogFileUpdates.Add(update);
            return Task.CompletedTask;
        }

        public Task<UserEntity> UpsertUserAsync(UserProfile profile, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ProjectEntity> CreateProjectAsync(string ownerUserId, string name, string? description, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProjectEntity>> ListProjectsAsync(string ownerUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ProjectEntity?> GetProjectAsync(string ownerUserId, string projectId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteProjectAsync(string ownerUserId, string projectId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<UploadSessionEntity> CreateUploadSessionAsync(UploadSessionCreateRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<UploadSessionEntity?> GetUploadSessionAsync(string uploadSessionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<UploadSessionEntity>> ListUploadSessionsAsync(string projectId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<LogFileEntity>> ListLogFilesAsync(string projectId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LogFileEntity?> GetLogFileAsync(string logFileId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ImportErrorEntity>> ListImportErrorsAsync(string uploadSessionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string?> CreateShareInviteAsync(string creatorUserId, string projectId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ProjectShareInvitePreview?> GetShareInvitePreviewAsync(string token, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ShareInviteAcceptResult> AcceptShareInviteAsync(string userId, string token, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingLogEventStore : ILogEventStore
    {
        public List<LogEvent> InsertedEvents { get; } = [];

        public Task InsertBatchAsync(IReadOnlyCollection<LogEvent> events, CancellationToken cancellationToken)
        {
            InsertedEvents.AddRange(events);
            return Task.CompletedTask;
        }

        public Task<LogEventSearchResult> SearchAsync(LogEventSearchRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LogEvent?> GetEventAsync(string eventId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<string, LogFileEventStats>> GetStatsByLogFileAsync(string projectId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<TimelinePoint>> GetTimelineAsync(TimelineRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IisAnalysisResult> GetIisAnalysisAsync(IisAnalysisRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NotUsedLogEventStore : ILogEventStore
    {
        public Task InsertBatchAsync(IReadOnlyCollection<LogEvent> events, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LogEventSearchResult> SearchAsync(LogEventSearchRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LogEvent?> GetEventAsync(string eventId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<string, LogFileEventStats>> GetStatsByLogFileAsync(string projectId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<TimelinePoint>> GetTimelineAsync(TimelineRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IisAnalysisResult> GetIisAnalysisAsync(IisAnalysisRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
