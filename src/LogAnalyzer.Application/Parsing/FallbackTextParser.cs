using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using LogAnalyzer.Application.Time;
using LogAnalyzer.Domain;

namespace LogAnalyzer.Application.Parsing;

public sealed class FallbackTextParser : ILogParser
{
    private static readonly Regex TimestampPattern = new(
        @"(?<timestamp>\d{4}[-/]\d{2}[-/]\d{2}\s+\d{2}:\d{2}:\d{2}(?:\.\d+)?|\d{2}\.\d{2}\.\d{4}\s+\d{2}:\d{2}:\d{2}(?:\.\d+)?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string Name => "Fallback text";
    public string Format => LogFormats.Unknown;

    public bool CanParse(LogSample sample)
    {
        return true;
    }

    public async IAsyncEnumerable<LogEvent> ParseAsync(
        Stream stream,
        LogParserContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var lineNumber = 0L;
        var byteOffset = 0L;
        PendingRaw? pending = null;
        var fallbackTimestamp = DateTimeOffset.UtcNow;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lineNumber++;
            var lineOffset = byteOffset;
            byteOffset += Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
            var timestampUtc = TryExtractTimestamp(line, context, out var extracted)
                ? extracted
                : fallbackTimestamp;

            if (TryExtractTimestamp(line, context, out _))
            {
                if (pending is not null)
                {
                    yield return pending.ToLogEvent(lineNumber - 1);
                }

                pending = new PendingRaw(context, timestampUtc, lineNumber, lineOffset, line);
                continue;
            }

            if (pending is not null && context.CombineMultilineExceptions)
            {
                pending.Append(line);
                continue;
            }

            yield return new PendingRaw(context, timestampUtc, lineNumber, lineOffset, line).ToLogEvent(lineNumber);
        }

        if (pending is not null)
        {
            yield return pending.ToLogEvent(lineNumber);
        }
    }

    private static bool TryExtractTimestamp(
        string line,
        LogParserContext context,
        out DateTimeOffset timestampUtc)
    {
        timestampUtc = default;
        var match = TimestampPattern.Match(line);
        if (!match.Success ||
            !TimeZoneHelper.TryParseLogTimestamp(match.Groups["timestamp"].Value, out var timestamp))
        {
            return false;
        }

        timestampUtc = TimeZoneHelper.ConvertLocalToUtc(timestamp, context.TimeZoneId);
        return true;
    }

    private sealed class PendingRaw
    {
        private readonly LogParserContext _context;
        private readonly DateTimeOffset _timestampUtc;
        private readonly long _lineNumber;
        private readonly long _byteOffset;
        private readonly StringBuilder _rawText;

        public PendingRaw(
            LogParserContext context,
            DateTimeOffset timestampUtc,
            long lineNumber,
            long byteOffset,
            string line)
        {
            _context = context;
            _timestampUtc = timestampUtc;
            _lineNumber = lineNumber;
            _byteOffset = byteOffset;
            _rawText = new StringBuilder(line);
        }

        public void Append(string line)
        {
            _rawText.AppendLine().Append(line);
        }

        public LogEvent ToLogEvent(long endLineNumber)
        {
            var raw = _rawText.ToString();
            return new LogEvent
            {
                Id = Guid.NewGuid().ToString("N"),
                ProjectId = _context.ProjectId,
                UploadSessionId = _context.UploadSessionId,
                LogFileId = _context.LogFileId,
                TimestampUtc = _timestampUtc,
                TimestampMs = TimeZoneHelper.ToUnixMilliseconds(_timestampUtc),
                Level = string.Empty,
                Source = "raw",
                ThreadId = string.Empty,
                LineNumber = _lineNumber,
                EndLineNumber = endLineNumber,
                ByteOffset = _byteOffset,
                Message = raw.Length > 500 ? raw[..500] : raw,
                Exception = string.Empty,
                RawText = raw
            };
        }
    }
}
