using LogAnalyzer.Domain;

namespace LogAnalyzer.Application;

public interface IMetadataRepository
{
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
