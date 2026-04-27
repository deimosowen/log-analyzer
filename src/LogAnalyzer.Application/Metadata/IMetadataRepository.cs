using LogAnalyzer.Domain;

namespace LogAnalyzer.Application;

public interface IMetadataRepository
{
    Task<UserEntity> UpsertUserAsync(UserProfile profile, CancellationToken cancellationToken);
    Task<ProjectEntity> CreateProjectAsync(string ownerUserId, string name, string? description, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProjectEntity>> ListProjectsAsync(string ownerUserId, CancellationToken cancellationToken);
    Task<ProjectEntity?> GetProjectAsync(string ownerUserId, string projectId, CancellationToken cancellationToken);
    Task DeleteProjectAsync(string ownerUserId, string projectId, CancellationToken cancellationToken);
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
