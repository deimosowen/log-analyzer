namespace LogAnalyzer.Application;

public interface IImportJobQueue
{
    ValueTask EnqueueAsync(ImportJob job, CancellationToken cancellationToken);
    ValueTask<ImportJob> DequeueAsync(CancellationToken cancellationToken);
}
