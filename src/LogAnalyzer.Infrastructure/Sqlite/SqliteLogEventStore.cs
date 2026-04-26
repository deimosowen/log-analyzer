using System.Globalization;
using System.Text;
using LogAnalyzer.Application;
using LogAnalyzer.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.Infrastructure.Sqlite;

public sealed class SqliteLogEventStore : ILogEventStore
{
    private readonly string _connectionString;

    public SqliteLogEventStore(IOptions<SqliteOptions> options)
    {
        var databasePath = Path.GetFullPath(options.Value.DatabasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS log_events (
                event_id TEXT PRIMARY KEY,
                project_id TEXT NOT NULL,
                upload_session_id TEXT NOT NULL,
                log_file_id TEXT NOT NULL,
                timestamp_utc TEXT NOT NULL,
                timestamp_ms INTEGER NOT NULL,
                level TEXT NOT NULL,
                source TEXT NOT NULL,
                thread_id TEXT NOT NULL,
                line_number INTEGER NOT NULL,
                end_line_number INTEGER NOT NULL,
                byte_offset INTEGER NOT NULL,
                message TEXT NOT NULL,
                exception TEXT NOT NULL,
                raw_text TEXT NOT NULL,
                http_method TEXT NOT NULL,
                url TEXT NOT NULL,
                status_code INTEGER NOT NULL,
                client_ip TEXT NOT NULL,
                server_ip TEXT NOT NULL,
                user_agent TEXT NOT NULL,
                time_taken INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_log_events_project_time ON log_events(project_id, timestamp_ms, log_file_id, line_number);
            CREATE INDEX IF NOT EXISTS ix_log_events_log_file ON log_events(log_file_id, timestamp_ms);
            CREATE INDEX IF NOT EXISTS ix_log_events_project_level ON log_events(project_id, level, timestamp_ms);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task InsertBatchAsync(IReadOnlyCollection<LogEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            INSERT OR REPLACE INTO log_events
                (event_id, project_id, upload_session_id, log_file_id, timestamp_utc, timestamp_ms,
                 level, source, thread_id, line_number, end_line_number, byte_offset, message, exception, raw_text,
                 http_method, url, status_code, client_ip, server_ip, user_agent, time_taken)
            VALUES
                ($event_id, $project_id, $upload_session_id, $log_file_id, $timestamp_utc, $timestamp_ms,
                 $level, $source, $thread_id, $line_number, $end_line_number, $byte_offset, $message, $exception, $raw_text,
                 $http_method, $url, $status_code, $client_ip, $server_ip, $user_agent, $time_taken);
            """;

        foreach (var logEvent in events)
        {
            command.Parameters.Clear();
            AddEventParameters(command, logEvent);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<LogEventSearchResult> SearchAsync(LogEventSearchRequest request, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit, 1, 1000);
        var offset = Math.Max(0, request.Offset);
        var query = BuildWhere(request);

        await using var connection = await OpenAsync(cancellationToken);
        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = $"SELECT COUNT(*) FROM log_events {query.WhereSql};";
        foreach (var parameter in query.Parameters)
        {
            countCommand.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        var total = Convert.ToInt64(await countCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT *
            FROM log_events
            {query.WhereSql}
            ORDER BY timestamp_ms, log_file_id, line_number
            LIMIT $limit OFFSET $offset;
            """;
        foreach (var parameter in query.Parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        command.Parameters.AddWithValue("$limit", limit);
        command.Parameters.AddWithValue("$offset", offset);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var events = new List<LogEvent>();
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(ReadEvent(reader));
        }

        return new LogEventSearchResult(events, total);
    }

    public async Task<LogEvent?> GetEventAsync(string eventId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM log_events WHERE event_id = $id;";
        command.Parameters.AddWithValue("$id", eventId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadEvent(reader) : null;
    }

    public async Task<IReadOnlyDictionary<string, LogFileEventStats>> GetStatsByLogFileAsync(string projectId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT log_file_id,
                   COUNT(*) AS event_count,
                   SUM(CASE WHEN level = 'ERROR' OR level = 'FATAL' THEN 1 ELSE 0 END) AS error_count,
                   SUM(CASE WHEN level = 'WARN' THEN 1 ELSE 0 END) AS warn_count,
                   MIN(timestamp_utc) AS first_timestamp,
                   MAX(timestamp_utc) AS last_timestamp
            FROM log_events
            WHERE project_id = $project_id
            GROUP BY log_file_id;
            """;
        command.Parameters.AddWithValue("$project_id", projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new Dictionary<string, LogFileEventStats>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            var logFileId = reader.GetString(reader.GetOrdinal("log_file_id"));
            result[logFileId] = new LogFileEventStats(
                logFileId,
                reader.GetInt64(reader.GetOrdinal("event_count")),
                reader.GetInt64(reader.GetOrdinal("error_count")),
                reader.GetInt64(reader.GetOrdinal("warn_count")),
                FromDb(reader.GetString(reader.GetOrdinal("first_timestamp"))),
                FromDb(reader.GetString(reader.GetOrdinal("last_timestamp"))));
        }

        return result;
    }

    public async Task<IReadOnlyList<TimelinePoint>> GetTimelineAsync(TimelineRequest request, CancellationToken cancellationToken)
    {
        var bucketMs = Math.Max(1000L, (long)request.Bucket.TotalMilliseconds);
        var query = BuildWhere(new LogEventSearchRequest
        {
            ProjectId = request.ProjectId,
            FromUtc = request.FromUtc,
            ToUtc = request.ToUtc,
            LogFileIds = request.LogFileIds,
            Levels = request.Levels,
            Limit = 1
        });

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT (timestamp_ms / $bucket_ms) * $bucket_ms AS bucket_ms,
                   level,
                   COUNT(*) AS count
            FROM log_events
            {query.WhereSql}
            GROUP BY bucket_ms, level
            ORDER BY bucket_ms, level;
            """;
        foreach (var parameter in query.Parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        command.Parameters.AddWithValue("$bucket_ms", bucketMs);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<TimelinePoint>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var bucket = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(reader.GetOrdinal("bucket_ms")));
            result.Add(new TimelinePoint(
                bucket,
                reader.GetString(reader.GetOrdinal("level")),
                reader.GetInt64(reader.GetOrdinal("count"))));
        }

        return result;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static QueryParts BuildWhere(LogEventSearchRequest request)
    {
        var conditions = new List<string> { "project_id = $project_id" };
        var parameters = new List<SqlParameterValue> { new("$project_id", request.ProjectId) };

        var from = request.FromUtc;
        var to = request.ToUtc;
        if (request.AroundUtc is not null)
        {
            from = request.AroundUtc.Value.AddSeconds(-Math.Max(0, request.BeforeSeconds));
            to = request.AroundUtc.Value.AddSeconds(Math.Max(0, request.AfterSeconds));
        }

        if (from is not null)
        {
            conditions.Add("timestamp_ms >= $from_ms");
            parameters.Add(new SqlParameterValue("$from_ms", from.Value.ToUnixTimeMilliseconds()));
        }

        if (to is not null)
        {
            conditions.Add("timestamp_ms <= $to_ms");
            parameters.Add(new SqlParameterValue("$to_ms", to.Value.ToUnixTimeMilliseconds()));
        }

        AddInCondition("log_file_id", "$log_file_id", request.LogFileIds, conditions, parameters);
        AddInCondition("level", "$level", request.Levels.Select(LogLevels.Normalize).Where(static x => x.Length > 0).ToArray(), conditions, parameters);

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            conditions.Add("(message LIKE $query OR raw_text LIKE $query OR exception LIKE $query OR url LIKE $query)");
            parameters.Add(new SqlParameterValue("$query", $"%{request.Query.Trim()}%"));
        }

        if (!string.IsNullOrWhiteSpace(request.ThreadId))
        {
            conditions.Add("thread_id = $thread_id");
            parameters.Add(new SqlParameterValue("$thread_id", request.ThreadId.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            conditions.Add("source LIKE $source");
            parameters.Add(new SqlParameterValue("$source", $"%{request.Source.Trim()}%"));
        }

        if (!string.IsNullOrWhiteSpace(request.HttpMethod))
        {
            conditions.Add("http_method = $http_method");
            parameters.Add(new SqlParameterValue("$http_method", request.HttpMethod.Trim().ToUpperInvariant()));
        }

        if (!string.IsNullOrWhiteSpace(request.Url))
        {
            conditions.Add("url LIKE $url");
            parameters.Add(new SqlParameterValue("$url", $"%{request.Url.Trim()}%"));
        }

        if (request.StatusCodeClass is not null)
        {
            var start = request.StatusCodeClass.Value * 100;
            conditions.Add("status_code >= $status_start AND status_code < $status_end");
            parameters.Add(new SqlParameterValue("$status_start", start));
            parameters.Add(new SqlParameterValue("$status_end", start + 100));
        }

        return new QueryParts("WHERE " + string.Join(" AND ", conditions), parameters);
    }

    private static void AddInCondition(
        string column,
        string prefix,
        IReadOnlyCollection<string> values,
        ICollection<string> conditions,
        ICollection<SqlParameterValue> parameters)
    {
        if (values.Count == 0)
        {
            return;
        }

        var names = new List<string>(values.Count);
        var index = 0;
        foreach (var value in values.Where(static value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var name = $"{prefix}_{index++}";
            names.Add(name);
            parameters.Add(new SqlParameterValue(name, value));
        }

        if (names.Count > 0)
        {
            conditions.Add($"{column} IN ({string.Join(", ", names)})");
        }
    }

    private static void AddEventParameters(SqliteCommand command, LogEvent logEvent)
    {
        command.Parameters.AddWithValue("$event_id", logEvent.Id);
        command.Parameters.AddWithValue("$project_id", logEvent.ProjectId);
        command.Parameters.AddWithValue("$upload_session_id", logEvent.UploadSessionId);
        command.Parameters.AddWithValue("$log_file_id", logEvent.LogFileId);
        command.Parameters.AddWithValue("$timestamp_utc", ToDb(logEvent.TimestampUtc));
        command.Parameters.AddWithValue("$timestamp_ms", logEvent.TimestampMs);
        command.Parameters.AddWithValue("$level", logEvent.Level);
        command.Parameters.AddWithValue("$source", logEvent.Source);
        command.Parameters.AddWithValue("$thread_id", logEvent.ThreadId);
        command.Parameters.AddWithValue("$line_number", logEvent.LineNumber);
        command.Parameters.AddWithValue("$end_line_number", logEvent.EndLineNumber);
        command.Parameters.AddWithValue("$byte_offset", logEvent.ByteOffset);
        command.Parameters.AddWithValue("$message", logEvent.Message);
        command.Parameters.AddWithValue("$exception", logEvent.Exception);
        command.Parameters.AddWithValue("$raw_text", logEvent.RawText);
        command.Parameters.AddWithValue("$http_method", logEvent.HttpMethod);
        command.Parameters.AddWithValue("$url", logEvent.Url);
        command.Parameters.AddWithValue("$status_code", logEvent.StatusCode);
        command.Parameters.AddWithValue("$client_ip", logEvent.ClientIp);
        command.Parameters.AddWithValue("$server_ip", logEvent.ServerIp);
        command.Parameters.AddWithValue("$user_agent", logEvent.UserAgent);
        command.Parameters.AddWithValue("$time_taken", logEvent.TimeTaken);
    }

    private static LogEvent ReadEvent(SqliteDataReader reader)
    {
        return new LogEvent
        {
            Id = reader.GetString(reader.GetOrdinal("event_id")),
            ProjectId = reader.GetString(reader.GetOrdinal("project_id")),
            UploadSessionId = reader.GetString(reader.GetOrdinal("upload_session_id")),
            LogFileId = reader.GetString(reader.GetOrdinal("log_file_id")),
            TimestampUtc = FromDb(reader.GetString(reader.GetOrdinal("timestamp_utc"))),
            TimestampMs = reader.GetInt64(reader.GetOrdinal("timestamp_ms")),
            Level = reader.GetString(reader.GetOrdinal("level")),
            Source = reader.GetString(reader.GetOrdinal("source")),
            ThreadId = reader.GetString(reader.GetOrdinal("thread_id")),
            LineNumber = reader.GetInt64(reader.GetOrdinal("line_number")),
            EndLineNumber = reader.GetInt64(reader.GetOrdinal("end_line_number")),
            ByteOffset = reader.GetInt64(reader.GetOrdinal("byte_offset")),
            Message = reader.GetString(reader.GetOrdinal("message")),
            Exception = reader.GetString(reader.GetOrdinal("exception")),
            RawText = reader.GetString(reader.GetOrdinal("raw_text")),
            HttpMethod = reader.GetString(reader.GetOrdinal("http_method")),
            Url = reader.GetString(reader.GetOrdinal("url")),
            StatusCode = reader.GetInt32(reader.GetOrdinal("status_code")),
            ClientIp = reader.GetString(reader.GetOrdinal("client_ip")),
            ServerIp = reader.GetString(reader.GetOrdinal("server_ip")),
            UserAgent = reader.GetString(reader.GetOrdinal("user_agent")),
            TimeTaken = reader.GetInt32(reader.GetOrdinal("time_taken"))
        };
    }

    private static string ToDb(DateTimeOffset timestamp)
    {
        return timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset FromDb(string value)
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture).ToUniversalTime();
    }

    private sealed record QueryParts(string WhereSql, IReadOnlyList<SqlParameterValue> Parameters);
    private sealed record SqlParameterValue(string Name, object? Value);
}
