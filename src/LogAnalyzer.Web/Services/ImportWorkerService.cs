using LogAnalyzer.Application;
using LogAnalyzer.Domain;

namespace LogAnalyzer.Web.Services;

public sealed class ImportWorkerService : BackgroundService
{
    private readonly IImportJobQueue _queue;
    private readonly ImportProcessor _processor;
    private readonly IMetadataRepository _metadataRepository;
    private readonly ImportCancellationRegistry _cancellationRegistry;
    private readonly ILogger<ImportWorkerService> _logger;

    public ImportWorkerService(
        IImportJobQueue queue,
        ImportProcessor processor,
        IMetadataRepository metadataRepository,
        ImportCancellationRegistry cancellationRegistry,
        ILogger<ImportWorkerService> logger)
    {
        _queue = queue;
        _processor = processor;
        _metadataRepository = metadataRepository;
        _cancellationRegistry = cancellationRegistry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await _queue.DequeueAsync(stoppingToken);
                var upload = await _metadataRepository.GetUploadSessionAsync(job.UploadSessionId, stoppingToken);
                if (upload?.Status == UploadStatuses.Cancelled)
                {
                    continue;
                }

                using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                _cancellationRegistry.Register(job.UploadSessionId, linked);
                try
                {
                    await _processor.ProcessAsync(job, linked.Token);
                }
                finally
                {
                    _cancellationRegistry.Unregister(job.UploadSessionId);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Import job was cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled import worker error.");
            }
        }
    }
}
