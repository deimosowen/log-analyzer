using LogAnalyzer.Application;
using LogAnalyzer.Domain;
using LogAnalyzer.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.Tests;

public sealed class SqliteLogEventStoreTimelineTests
{
    [Fact]
    public async Task GetTimelineAsyncAggregatesByBucketAndLevel()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"log-analyzer-tests-{Guid.NewGuid():N}", "events.db");
        try
        {
            var factory = new SqliteConnectionFactory(Options.Create(new SqliteOptions { DatabasePath = databasePath }));
            var migrator = new SqliteEventStoreMigrator(factory);
            await migrator.MigrateAsync(CancellationToken.None);

            var store = new SqliteLogEventStore(factory);
            var projectId = Guid.NewGuid().ToString("N");
            var uploadId = Guid.NewGuid().ToString("N");
            var logFileId = Guid.NewGuid().ToString("N");
            var start = DateTimeOffset.Parse("2026-04-21T05:45:00Z");

            await store.InsertBatchAsync(
                [
                    CreateEvent(projectId, uploadId, logFileId, start.AddSeconds(2), LogLevels.Error),
                    CreateEvent(projectId, uploadId, logFileId, start.AddSeconds(7), LogLevels.Warn),
                    CreateEvent(projectId, uploadId, logFileId, start.AddSeconds(11), LogLevels.Error),
                    CreateEvent(projectId, uploadId, logFileId, start.AddSeconds(12), LogLevels.Info)
                ],
                CancellationToken.None);

            var points = await store.GetTimelineAsync(new TimelineRequest
            {
                ProjectId = projectId,
                FromUtc = start,
                ToUtc = start.AddSeconds(30),
                Bucket = TimeSpan.FromSeconds(10),
                Levels = [LogLevels.Error, LogLevels.Warn]
            }, CancellationToken.None);

            Assert.Equal(3, points.Count);
            Assert.Contains(points, point => point.BucketUtc == start && point.Level == LogLevels.Error && point.Count == 1);
            Assert.Contains(points, point => point.BucketUtc == start && point.Level == LogLevels.Warn && point.Count == 1);
            Assert.Contains(points, point => point.BucketUtc == start.AddSeconds(10) && point.Level == LogLevels.Error && point.Count == 1);
            Assert.DoesNotContain(points, point => point.Level == LogLevels.Info);
        }
        finally
        {
            DeleteDatabaseDirectory(databasePath);
        }
    }

    private static LogEvent CreateEvent(
        string projectId,
        string uploadId,
        string logFileId,
        DateTimeOffset timestamp,
        string level)
    {
        return new LogEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            ProjectId = projectId,
            UploadSessionId = uploadId,
            LogFileId = logFileId,
            TimestampUtc = timestamp,
            TimestampMs = timestamp.ToUnixTimeMilliseconds(),
            Level = level,
            Source = "Test",
            ThreadId = "1",
            LineNumber = 1,
            EndLineNumber = 1,
            Message = $"{level} message",
            RawText = $"{timestamp:O} {level} message"
        };
    }

    private static void DeleteDatabaseDirectory(string databasePath)
    {
        SqliteConnection.ClearAllPools();
        var directory = Path.GetDirectoryName(databasePath);
        if (directory is not null && Directory.Exists(directory))
        {
            TryDeleteDirectory(directory);
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 2)
            {
                Thread.Sleep(100);
            }
        }
    }
}
