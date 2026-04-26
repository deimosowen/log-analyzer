using System.Text;
using System.Text.Json;
using LogAnalyzer.Application;
using LogAnalyzer.Domain;

namespace LogAnalyzer.Infrastructure.ClickHouse;

public sealed class ClickHouseLogEventStore : ILogEventStore
{
    private readonly ClickHouseSqlClient _client;
    private readonly string _table;

    public ClickHouseLogEventStore(ClickHouseSqlClient client)
    {
        _client = client;
        _table = client.EventTable;
    }

    public async Task InsertBatchAsync(IReadOnlyCollection<LogEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"INSERT INTO {_table} FORMAT JSONEachRow");
        foreach (var logEvent in events)
        {
            var row = new Dictionary<string, object?>
            {
                ["event_id"] = logEvent.Id,
                ["project_id"] = ClickHouseSql.ToUuid(logEvent.ProjectId),
                ["upload_session_id"] = ClickHouseSql.ToUuid(logEvent.UploadSessionId),
                ["log_file_id"] = ClickHouseSql.ToUuid(logEvent.LogFileId),
                ["timestamp_utc"] = ClickHouseSql.ToDateTime64(logEvent.TimestampUtc),
                ["timestamp_ms"] = logEvent.TimestampMs,
                ["level"] = logEvent.Level,
                ["source"] = logEvent.Source,
                ["thread_id"] = logEvent.ThreadId,
                ["line_number"] = logEvent.LineNumber,
                ["end_line_number"] = logEvent.EndLineNumber,
                ["byte_offset"] = logEvent.ByteOffset,
                ["message"] = logEvent.Message,
                ["exception"] = logEvent.Exception,
                ["raw_text"] = logEvent.RawText,
                ["http_method"] = logEvent.HttpMethod,
                ["url"] = logEvent.Url,
                ["status_code"] = logEvent.StatusCode,
                ["client_ip"] = logEvent.ClientIp,
                ["server_ip"] = logEvent.ServerIp,
                ["user_agent"] = logEvent.UserAgent,
                ["time_taken"] = logEvent.TimeTaken
            };
            builder.AppendLine(JsonSerializer.Serialize(row));
        }

        await _client.ExecuteAsync(builder.ToString(), cancellationToken);
    }

    public async Task<LogEventSearchResult> SearchAsync(LogEventSearchRequest request, CancellationToken cancellationToken)
    {
        var where = BuildWhere(request);
        var limit = Math.Clamp(request.Limit, 1, EventSearchDefaults.MaxLimit);
        var offset = Math.Max(0, request.Offset);
        var countRows = await _client.QueryJsonEachRowAsync($"SELECT count() AS c FROM {_table} {where} FORMAT JSONEachRow", cancellationToken);
        var total = countRows.Count == 0 ? 0 : countRows[0].GetProperty("c").GetInt64();

        var sql = $$"""
            SELECT *
            FROM {{_table}}
            {{where}}
            ORDER BY timestamp_utc, log_file_id, line_number
            LIMIT {{limit}} OFFSET {{offset}}
            FORMAT JSONEachRow
            """;
        var rows = await _client.QueryJsonEachRowAsync(sql, cancellationToken);
        return new LogEventSearchResult(rows.Select(ReadEvent).ToArray(), total);
    }

    public async Task<LogEvent?> GetEventAsync(string eventId, CancellationToken cancellationToken)
    {
        var rows = await _client.QueryJsonEachRowAsync(
            $"SELECT * FROM {_table} WHERE event_id = {ClickHouseSql.QuoteLiteral(eventId)} LIMIT 1 FORMAT JSONEachRow",
            cancellationToken);
        return rows.Count == 0 ? null : ReadEvent(rows[0]);
    }

    public async Task<IReadOnlyDictionary<string, LogFileEventStats>> GetStatsByLogFileAsync(string projectId, CancellationToken cancellationToken)
    {
        var sql = $$"""
            SELECT
                toString(log_file_id) AS log_file_id,
                count() AS event_count,
                countIf(level = 'ERROR' OR level = 'FATAL') AS error_count,
                countIf(level = 'WARN') AS warn_count,
                min(timestamp_utc) AS first_timestamp,
                max(timestamp_utc) AS last_timestamp
            FROM {{_table}}
            WHERE project_id = toUUID({{ClickHouseSql.QuoteLiteral(ClickHouseSql.ToUuid(projectId))}})
            GROUP BY log_file_id
            FORMAT JSONEachRow
            """;
        var rows = await _client.QueryJsonEachRowAsync(sql, cancellationToken);
        return rows.ToDictionary(
            row => row.GetProperty("log_file_id").GetString()!.Replace("-", string.Empty, StringComparison.Ordinal),
            row => new LogFileEventStats(
                row.GetProperty("log_file_id").GetString()!.Replace("-", string.Empty, StringComparison.Ordinal),
                row.GetProperty("event_count").GetInt64(),
                row.GetProperty("error_count").GetInt64(),
                row.GetProperty("warn_count").GetInt64(),
                ParseTimestamp(row.GetProperty("first_timestamp")),
                ParseTimestamp(row.GetProperty("last_timestamp"))),
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<TimelinePoint>> GetTimelineAsync(TimelineRequest request, CancellationToken cancellationToken)
    {
        var search = new LogEventSearchRequest
        {
            ProjectId = request.ProjectId,
            FromUtc = request.FromUtc,
            ToUtc = request.ToUtc,
            LogFileIds = request.LogFileIds,
            Levels = request.Levels
        };
        var where = BuildWhere(search);
        var seconds = Math.Max(1, (int)request.Bucket.TotalSeconds);
        var sql = $$"""
            SELECT
                toStartOfInterval(timestamp_utc, INTERVAL {{seconds}} second) AS bucket,
                level,
                count() AS count
            FROM {{_table}}
            {{where}}
            GROUP BY bucket, level
            ORDER BY bucket, level
            FORMAT JSONEachRow
            """;
        var rows = await _client.QueryJsonEachRowAsync(sql, cancellationToken);
        return rows.Select(row => new TimelinePoint(
            ParseTimestamp(row.GetProperty("bucket")),
            row.GetProperty("level").GetString() ?? string.Empty,
            row.GetProperty("count").GetInt64())).ToArray();
    }

    private string BuildWhere(LogEventSearchRequest request)
    {
        var conditions = new List<string>
        {
            $"project_id = toUUID({ClickHouseSql.QuoteLiteral(ClickHouseSql.ToUuid(request.ProjectId))})"
        };
        var from = request.FromUtc;
        var to = request.ToUtc;
        if (request.AroundUtc is not null)
        {
            from = request.AroundUtc.Value.AddSeconds(-Math.Max(0, request.BeforeSeconds));
            to = request.AroundUtc.Value.AddSeconds(Math.Max(0, request.AfterSeconds));
        }

        if (from is not null)
        {
            conditions.Add($"timestamp_utc >= toDateTime64({ClickHouseSql.QuoteLiteral(ClickHouseSql.ToDateTime64(from.Value))}, 4, 'UTC')");
        }

        if (to is not null)
        {
            conditions.Add($"timestamp_utc <= toDateTime64({ClickHouseSql.QuoteLiteral(ClickHouseSql.ToDateTime64(to.Value))}, 4, 'UTC')");
        }

        if (request.LogFileIds.Count > 0)
        {
            conditions.Add("log_file_id IN (" +
                           string.Join(", ", request.LogFileIds.Select(id => $"toUUID({ClickHouseSql.QuoteLiteral(ClickHouseSql.ToUuid(id))})")) +
                           ")");
        }

        var levels = request.Levels.Select(LogLevels.Normalize).Where(static x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (levels.Length > 0)
        {
            conditions.Add("level IN (" + string.Join(", ", levels.Select(ClickHouseSql.QuoteLiteral)) + ")");
        }

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var query = ClickHouseSql.QuoteLiteral($"%{request.Query.Trim()}%");
            conditions.Add($"(message LIKE {query} OR raw_text LIKE {query} OR exception LIKE {query} OR url LIKE {query})");
        }

        if (!string.IsNullOrWhiteSpace(request.ThreadId))
        {
            conditions.Add($"thread_id = {ClickHouseSql.QuoteLiteral(request.ThreadId.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            conditions.Add($"source LIKE {ClickHouseSql.QuoteLiteral($"%{request.Source.Trim()}%")}");
        }

        if (!string.IsNullOrWhiteSpace(request.HttpMethod))
        {
            conditions.Add($"http_method = {ClickHouseSql.QuoteLiteral(request.HttpMethod.Trim().ToUpperInvariant())}");
        }

        if (!string.IsNullOrWhiteSpace(request.Url))
        {
            conditions.Add($"url LIKE {ClickHouseSql.QuoteLiteral($"%{request.Url.Trim()}%")}");
        }

        if (request.StatusCodeClass is not null)
        {
            var start = request.StatusCodeClass.Value * 100;
            conditions.Add($"status_code >= {start} AND status_code < {start + 100}");
        }

        return "WHERE " + string.Join(" AND ", conditions);
    }

    private static LogEvent ReadEvent(JsonElement row)
    {
        var timestamp = ParseTimestamp(row.GetProperty("timestamp_utc"));
        return new LogEvent
        {
            Id = GetString(row, "event_id"),
            ProjectId = GetString(row, "project_id").Replace("-", string.Empty, StringComparison.Ordinal),
            UploadSessionId = GetString(row, "upload_session_id").Replace("-", string.Empty, StringComparison.Ordinal),
            LogFileId = GetString(row, "log_file_id").Replace("-", string.Empty, StringComparison.Ordinal),
            TimestampUtc = timestamp,
            TimestampMs = GetInt64(row, "timestamp_ms"),
            Level = GetString(row, "level"),
            Source = GetString(row, "source"),
            ThreadId = GetString(row, "thread_id"),
            LineNumber = GetInt64(row, "line_number"),
            EndLineNumber = GetInt64(row, "end_line_number"),
            ByteOffset = GetInt64(row, "byte_offset"),
            Message = GetString(row, "message"),
            Exception = GetString(row, "exception"),
            RawText = GetString(row, "raw_text"),
            HttpMethod = GetString(row, "http_method"),
            Url = GetString(row, "url"),
            StatusCode = GetInt32(row, "status_code"),
            ClientIp = GetString(row, "client_ip"),
            ServerIp = GetString(row, "server_ip"),
            UserAgent = GetString(row, "user_agent"),
            TimeTaken = GetInt32(row, "time_taken")
        };
    }

    private static string GetString(JsonElement row, string name)
    {
        return row.TryGetProperty(name, out var property) ? property.ToString() : string.Empty;
    }

    private static long GetInt64(JsonElement row, string name)
    {
        return row.TryGetProperty(name, out var property) && property.TryGetInt64(out var value) ? value : 0;
    }

    private static int GetInt32(JsonElement row, string name)
    {
        return row.TryGetProperty(name, out var property) && property.TryGetInt32(out var value) ? value : 0;
    }

    private static DateTimeOffset ParseTimestamp(JsonElement value)
    {
        return DateTimeOffset.TryParse(value.ToString(), out var timestamp)
            ? timestamp.ToUniversalTime()
            : DateTimeOffset.UnixEpoch;
    }

}
