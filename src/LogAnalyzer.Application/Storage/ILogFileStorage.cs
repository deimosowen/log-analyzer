namespace LogAnalyzer.Application;

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
