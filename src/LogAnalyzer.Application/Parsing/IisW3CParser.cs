using System.Runtime.CompilerServices;
using System.Text;
using LogAnalyzer.Application.Time;
using LogAnalyzer.Domain;

namespace LogAnalyzer.Application.Parsing;

public sealed class IisW3CParser : ILogParser
{
    private static readonly string[] DefaultFields =
    [
        "date",
        "time",
        "s-ip",
        "cs-method",
        "cs-uri-stem",
        "sc-status",
        "time-taken"
    ];

    public string Name => "IIS W3C";
    public string Format => LogFormats.IisW3C;

    public bool CanParse(LogSample sample)
    {
        return sample.Lines.Any(line => line.StartsWith("#Fields:", StringComparison.OrdinalIgnoreCase)) ||
               sample.Lines.Any(line => line.StartsWith("#Software: Microsoft Internet Information Services", StringComparison.OrdinalIgnoreCase));
    }

    public async IAsyncEnumerable<LogEvent> ParseAsync(
        Stream stream,
        LogParserContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var fields = DefaultFields;
        var lineNumber = 0L;
        var byteOffset = 0L;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lineNumber++;
            var lineOffset = byteOffset;
            byteOffset += Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("#Fields:", StringComparison.OrdinalIgnoreCase))
            {
                fields = line["#Fields:".Length..]
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                continue;
            }

            if (line.StartsWith('#'))
            {
                continue;
            }

            var values = line.Split(' ', StringSplitOptions.None);
            if (values.Length < 2)
            {
                continue;
            }

            var map = BuildMap(fields, values);
            if (!map.TryGetValue("date", out var date) ||
                !map.TryGetValue("time", out var time) ||
                !DateTime.TryParseExact(
                    $"{date} {time}",
                    ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss.FFFFFFF"],
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var timestamp))
            {
                continue;
            }

            var url = Get(map, "cs-uri-stem");
            var query = Get(map, "cs-uri-query");
            if (!string.IsNullOrWhiteSpace(query) && query != "-")
            {
                url = $"{url}?{query}";
            }

            _ = int.TryParse(Get(map, "sc-status"), out var statusCode);
            _ = int.TryParse(Get(map, "time-taken"), out var timeTaken);

            var message = $"{Get(map, "cs-method")} {url} {statusCode}".Trim();
            var timestampUtc = TimeZoneHelper.ConvertIisTime(timestamp, context);

            yield return new LogEvent
            {
                Id = Guid.NewGuid().ToString("N"),
                ProjectId = context.ProjectId,
                UploadSessionId = context.UploadSessionId,
                LogFileId = context.LogFileId,
                TimestampUtc = timestampUtc,
                TimestampMs = TimeZoneHelper.ToUnixMilliseconds(timestampUtc),
                Level = IisLevel(statusCode),
                Source = "IIS",
                ThreadId = string.Empty,
                LineNumber = lineNumber,
                EndLineNumber = lineNumber,
                ByteOffset = lineOffset,
                Message = message,
                Exception = string.Empty,
                RawText = line,
                HttpMethod = Get(map, "cs-method"),
                Url = url,
                StatusCode = statusCode,
                ClientIp = Get(map, "c-ip"),
                ServerIp = Get(map, "s-ip"),
                UserAgent = Get(map, "cs(User-Agent)"),
                TimeTaken = timeTaken
            };
        }
    }

    private static Dictionary<string, string> BuildMap(IReadOnlyList<string> fields, IReadOnlyList<string> values)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < fields.Count && i < values.Count; i++)
        {
            map[fields[i]] = values[i] == "-" ? string.Empty : Uri.UnescapeDataString(values[i]);
        }

        return map;
    }

    private static string Get(IReadOnlyDictionary<string, string> map, string key)
    {
        return map.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private static string IisLevel(int statusCode)
    {
        return statusCode >= 500 ? "ERROR" :
            statusCode >= 400 ? "WARN" :
            "INFO";
    }
}
