using LogAnalyzer.Domain;

namespace LogAnalyzer.Application;

public sealed record ImportOptions(
    string TimeZoneId,
    bool IisLogsAreUtc,
    bool CombineMultilineExceptions);

public sealed record ImportJob(
    string ProjectId,
    string UploadSessionId,
    ImportOptions Options);

public sealed record StoredUploadFile(
    string OriginalName,
    string StoredPath,
    long SizeBytes);

public sealed record ImportFileCandidate(
    string OriginalPath,
    string StoredPath,
    string DisplayName,
    long SizeBytes,
    string Hash);

public sealed record UploadSessionCreateRequest(
    string ProjectId,
    string? OriginalName);

public sealed record UploadProgressUpdate(
    string? Status = null,
    int? TotalFiles = null,
    int? ProcessedFiles = null,
    long? TotalLines = null,
    long? ProcessedLines = null,
    int? ErrorCount = null,
    DateTimeOffset? FinishedAt = null,
    string? CurrentFile = null);

public sealed record LogFileUpdate(
    string? Format = null,
    string? Status = null,
    DateTimeOffset? FirstTimestamp = null,
    DateTimeOffset? LastTimestamp = null,
    long? LineCount = null);

public sealed record LogEventSearchRequest
{
    public required string ProjectId { get; init; }
    public DateTimeOffset? FromUtc { get; init; }
    public DateTimeOffset? ToUtc { get; init; }
    public DateTimeOffset? AroundUtc { get; init; }
    public int BeforeSeconds { get; init; } = 30;
    public int AfterSeconds { get; init; } = 10;
    public IReadOnlyCollection<string> LogFileIds { get; init; } = [];
    public IReadOnlyCollection<string> Levels { get; init; } = [];
    public string? Query { get; init; }
    public string? ThreadId { get; init; }
    public string? Source { get; init; }
    public string? HttpMethod { get; init; }
    public string? Url { get; init; }
    public int? StatusCodeClass { get; init; }
    public int Offset { get; init; }
    public int Limit { get; init; } = 500;
}

public sealed record LogEventSearchResult(
    IReadOnlyList<LogEvent> Events,
    long TotalCount);

public sealed record TimelineRequest
{
    public required string ProjectId { get; init; }
    public required DateTimeOffset FromUtc { get; init; }
    public required DateTimeOffset ToUtc { get; init; }
    public required TimeSpan Bucket { get; init; }
    public IReadOnlyCollection<string> LogFileIds { get; init; } = [];
    public IReadOnlyCollection<string> Levels { get; init; } = [];
}

public sealed record LogSample(
    string FileName,
    IReadOnlyList<string> Lines);

public sealed record LogParserContext(
    string ProjectId,
    string UploadSessionId,
    string LogFileId,
    string TimeZoneId,
    bool IisLogsAreUtc,
    bool CombineMultilineExceptions);

public interface IMetadataRepository
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<ProjectEntity> CreateProjectAsync(string name, string? description, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProjectEntity>> ListProjectsAsync(CancellationToken cancellationToken);
    Task<ProjectEntity?> GetProjectAsync(string projectId, CancellationToken cancellationToken);
    Task DeleteProjectAsync(string projectId, CancellationToken cancellationToken);
    Task<UploadSessionEntity> CreateUploadSessionAsync(UploadSessionCreateRequest request, CancellationToken cancellationToken);
    Task<UploadSessionEntity?> GetUploadSessionAsync(string uploadSessionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<UploadSessionEntity>> ListUploadSessionsAsync(string projectId, CancellationToken cancellationToken);
    Task UpdateUploadSessionAsync(string uploadSessionId, UploadProgressUpdate update, CancellationToken cancellationToken);
    Task<LogFileEntity> CreateLogFileAsync(LogFileEntity logFile, CancellationToken cancellationToken);
    Task UpdateLogFileAsync(string logFileId, LogFileUpdate update, CancellationToken cancellationToken);
    Task<IReadOnlyList<LogFileEntity>> ListLogFilesAsync(string projectId, CancellationToken cancellationToken);
    Task<LogFileEntity?> GetLogFileAsync(string logFileId, CancellationToken cancellationToken);
    Task AddImportErrorAsync(ImportErrorEntity error, CancellationToken cancellationToken);
    Task<IReadOnlyList<ImportErrorEntity>> ListImportErrorsAsync(string uploadSessionId, CancellationToken cancellationToken);
}

public interface ILogEventStore
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task InsertBatchAsync(IReadOnlyCollection<LogEvent> events, CancellationToken cancellationToken);
    Task<LogEventSearchResult> SearchAsync(LogEventSearchRequest request, CancellationToken cancellationToken);
    Task<LogEvent?> GetEventAsync(string eventId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<string, LogFileEventStats>> GetStatsByLogFileAsync(string projectId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TimelinePoint>> GetTimelineAsync(TimelineRequest request, CancellationToken cancellationToken);
}

public interface ILogFileStorage
{
    string RootPath { get; }
    Task<StoredUploadFile> SaveOriginalAsync(
        string projectId,
        string uploadSessionId,
        string originalName,
        Stream content,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ImportFileCandidate>> DiscoverImportFilesAsync(
        string projectId,
        string uploadSessionId,
        CancellationToken cancellationToken);
}

public interface ILogParser
{
    string Name { get; }
    string Format { get; }
    bool CanParse(LogSample sample);

    IAsyncEnumerable<LogEvent> ParseAsync(
        Stream stream,
        LogParserContext context,
        CancellationToken cancellationToken);
}

public interface IImportJobQueue
{
    ValueTask EnqueueAsync(ImportJob job, CancellationToken cancellationToken);
    ValueTask<ImportJob> DequeueAsync(CancellationToken cancellationToken);
}
