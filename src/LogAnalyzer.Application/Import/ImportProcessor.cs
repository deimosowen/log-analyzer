using LogAnalyzer.Application.Parsing;
using LogAnalyzer.Domain;
using Microsoft.Extensions.Logging;

namespace LogAnalyzer.Application;

public sealed class ImportProcessor
{
    private readonly IMetadataRepository _metadataRepository;
    private readonly ILogEventStore _eventStore;
    private readonly ILogFileStorage _storage;
    private readonly LogParserSelector _parserSelector;
    private readonly ILogger<ImportProcessor> _logger;

    public ImportProcessor(
        IMetadataRepository metadataRepository,
        ILogEventStore eventStore,
        ILogFileStorage storage,
        LogParserSelector parserSelector,
        ILogger<ImportProcessor> logger)
    {
        _metadataRepository = metadataRepository;
        _eventStore = eventStore;
        _storage = storage;
        _parserSelector = parserSelector;
        _logger = logger;
    }

    public async Task ProcessAsync(ImportJob job, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting import {UploadSessionId}", job.UploadSessionId);

        try
        {
            await _metadataRepository.UpdateUploadSessionAsync(
                job.UploadSessionId,
                new UploadProgressUpdate(Status: UploadStatuses.Indexing),
                cancellationToken);

            var files = await _storage.DiscoverImportFilesAsync(job.ProjectId, job.UploadSessionId, cancellationToken);
            await _metadataRepository.UpdateUploadSessionAsync(
                job.UploadSessionId,
                new UploadProgressUpdate(TotalFiles: files.Count),
                cancellationToken);

            if (files.Count == 0)
            {
                const string message =
                    "Не найдено файлов логов для импорта. Поддерживаются отдельные файлы .log, .txt, .csv, .trace, .iis, .w3c и архивы .zip / .7z / .rar с такими файлами внутри.";

                await AddErrorAsync(
                    job.UploadSessionId,
                    null,
                    null,
                    message,
                    null,
                    cancellationToken);

                await _metadataRepository.UpdateUploadSessionAsync(
                    job.UploadSessionId,
                    new UploadProgressUpdate(
                        Status: UploadStatuses.Failed,
                        FinishedAt: DateTimeOffset.UtcNow,
                        ErrorCount: 1,
                        CurrentFile: string.Empty),
                    cancellationToken);

                _logger.LogWarning("Import {UploadSessionId} failed: no supported log files were found.", job.UploadSessionId);
                return;
            }

            var processedFiles = 0;
            var processedLines = 0L;
            var totalLines = 0L;

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _metadataRepository.UpdateUploadSessionAsync(
                    job.UploadSessionId,
                    new UploadProgressUpdate(CurrentFile: file.DisplayName),
                    cancellationToken);

                var logFile = new LogFileEntity(
                    Guid.NewGuid().ToString("N"),
                    job.ProjectId,
                    job.UploadSessionId,
                    file.OriginalPath,
                    file.StoredPath,
                    file.DisplayName,
                    file.SizeBytes,
                    file.Hash,
                    LogFormats.Unknown,
                    job.Options.TimeZoneId,
                    null,
                    null,
                    0,
                    LogFileStatuses.Indexing);

                await _metadataRepository.CreateLogFileAsync(logFile, cancellationToken);

                try
                {
                    var sample = await ReadSampleAsync(file.StoredPath, file.DisplayName, cancellationToken);
                    var parser = _parserSelector.Select(sample);
                    var isUnknown = parser.Format == LogFormats.Unknown;

                    await _metadataRepository.UpdateLogFileAsync(
                        logFile.Id,
                        new LogFileUpdate(Format: parser.Format),
                        cancellationToken);

                    if (isUnknown)
                    {
                        await AddErrorAsync(
                            job.UploadSessionId,
                            logFile.Id,
                            null,
                            "Log format was not recognized; fallback parser was used.",
                            null,
                            cancellationToken);
                    }

                    await using var stream = File.OpenRead(file.StoredPath);
                    var context = new LogParserContext(
                        job.ProjectId,
                        job.UploadSessionId,
                        logFile.Id,
                        job.Options.TimeZoneId,
                        job.Options.IisLogsAreUtc,
                        job.Options.CombineMultilineExceptions);

                    var batch = new List<LogEvent>(ImportProcessingDefaults.EventBatchSize);
                    DateTimeOffset? firstTimestamp = null;
                    DateTimeOffset? lastTimestamp = null;
                    var lineCount = 0L;

                    await foreach (var parsed in parser.ParseAsync(stream, context, cancellationToken))
                    {
                        batch.Add(parsed);
                        firstTimestamp ??= parsed.TimestampUtc;
                        lastTimestamp = parsed.TimestampUtc;
                        lineCount = Math.Max(lineCount, parsed.EndLineNumber);

                        if (batch.Count >= ImportProcessingDefaults.EventBatchSize)
                        {
                            await _eventStore.InsertBatchAsync(batch, cancellationToken);
                            batch.Clear();
                            await _metadataRepository.UpdateUploadSessionAsync(
                                job.UploadSessionId,
                                new UploadProgressUpdate(ProcessedLines: processedLines + lineCount),
                                cancellationToken);
                        }
                    }

                    if (batch.Count > 0)
                    {
                        await _eventStore.InsertBatchAsync(batch, cancellationToken);
                    }

                    processedFiles++;
                    processedLines += lineCount;
                    totalLines += lineCount;

                    await _metadataRepository.UpdateLogFileAsync(
                        logFile.Id,
                        new LogFileUpdate(
                            Status: isUnknown ? LogFileStatuses.Unknown : LogFileStatuses.Completed,
                            FirstTimestamp: firstTimestamp,
                            LastTimestamp: lastTimestamp,
                            LineCount: lineCount),
                        cancellationToken);

                    await _metadataRepository.UpdateUploadSessionAsync(
                        job.UploadSessionId,
                        new UploadProgressUpdate(
                            ProcessedFiles: processedFiles,
                            ProcessedLines: processedLines,
                            TotalLines: totalLines),
                        cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to import file {File}", file.StoredPath);
                    processedFiles++;

                    await AddErrorAsync(
                        job.UploadSessionId,
                        logFile.Id,
                        null,
                        ex.Message,
                        null,
                        cancellationToken);

                    await _metadataRepository.UpdateLogFileAsync(
                        logFile.Id,
                        new LogFileUpdate(Status: LogFileStatuses.Failed),
                        cancellationToken);

                    await _metadataRepository.UpdateUploadSessionAsync(
                        job.UploadSessionId,
                        new UploadProgressUpdate(ProcessedFiles: processedFiles),
                        cancellationToken);
                }
            }

            await _metadataRepository.UpdateUploadSessionAsync(
                job.UploadSessionId,
                new UploadProgressUpdate(
                    Status: UploadStatuses.Completed,
                    FinishedAt: DateTimeOffset.UtcNow,
                    CurrentFile: string.Empty),
                cancellationToken);

            _logger.LogInformation("Import {UploadSessionId} completed", job.UploadSessionId);
        }
        catch (OperationCanceledException)
        {
            await _metadataRepository.UpdateUploadSessionAsync(
                job.UploadSessionId,
                new UploadProgressUpdate(
                    Status: UploadStatuses.Cancelled,
                    FinishedAt: DateTimeOffset.UtcNow),
                CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import {UploadSessionId} failed", job.UploadSessionId);
            await AddErrorAsync(
                job.UploadSessionId,
                null,
                null,
                ex.Message,
                null,
                CancellationToken.None);

            await _metadataRepository.UpdateUploadSessionAsync(
                job.UploadSessionId,
                new UploadProgressUpdate(
                    Status: UploadStatuses.Failed,
                    FinishedAt: DateTimeOffset.UtcNow,
                    ErrorCount: 1),
                CancellationToken.None);
        }
    }

    private async Task AddErrorAsync(
        string uploadSessionId,
        string? logFileId,
        long? lineNumber,
        string message,
        string? rawText,
        CancellationToken cancellationToken)
    {
        await _metadataRepository.AddImportErrorAsync(
            new ImportErrorEntity(
                Guid.NewGuid().ToString("N"),
                uploadSessionId,
                logFileId,
                lineNumber,
                message,
                rawText,
                DateTimeOffset.UtcNow),
            cancellationToken);
    }

    private static async Task<LogSample> ReadSampleAsync(
        string path,
        string fileName,
        CancellationToken cancellationToken)
    {
        var lines = new List<string>(ImportProcessingDefaults.SampleLineCount);
        using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream);

        while (lines.Count < ImportProcessingDefaults.SampleLineCount &&
               await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }

        return new LogSample(fileName, lines);
    }
}
