using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using LogAnalyzer.Application.Time;
using LogAnalyzer.Domain;

namespace LogAnalyzer.Application.Parsing;

public abstract class RegexLineLogParser : ILogParser
{
    public abstract string Name { get; }
    public abstract string Format { get; }

    public abstract bool CanParse(LogSample sample);

    public async IAsyncEnumerable<LogEvent> ParseAsync(
        Stream stream,
        LogParserContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        PendingEvent? pending = null;
        var lineNumber = 0L;
        var byteOffset = 0L;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lineNumber++;
            var lineOffset = byteOffset;
            byteOffset += Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;

            if (TryParseStart(line, context, lineNumber, lineOffset, out var parsed))
            {
                if (pending is not null)
                {
                    yield return pending.ToLogEvent(lineNumber - 1);
                }

                pending = PendingEvent.From(parsed, context);
                continue;
            }

            if (pending is not null && context.CombineMultilineExceptions)
            {
                pending.AppendContinuation(line);
            }
        }

        if (pending is not null)
        {
            yield return pending.ToLogEvent(lineNumber);
        }
    }

    protected abstract bool TryParseStart(
        string line,
        LogParserContext context,
        long lineNumber,
        long byteOffset,
        out ParsedStart parsed);

    protected static DateTimeOffset ToUtc(DateTime timestamp, LogParserContext context)
    {
        return TimeZoneHelper.ConvertLocalToUtc(timestamp, context.TimeZoneId);
    }

    protected sealed record ParsedStart(
        DateTimeOffset TimestampUtc,
        string Level,
        string Source,
        string ThreadId,
        long LineNumber,
        long ByteOffset,
        string Message,
        string Exception,
        string RawText,
        string HttpMethod = "",
        string Url = "",
        int StatusCode = 0,
        string ClientIp = "",
        string ServerIp = "",
        string UserAgent = "",
        int TimeTaken = 0);

    private sealed class PendingEvent
    {
        private readonly ParsedStart _start;
        private readonly LogParserContext _context;
        private readonly StringBuilder _rawText;
        private readonly StringBuilder _exception;

        private PendingEvent(ParsedStart start, LogParserContext context)
        {
            _start = start;
            _context = context;
            _rawText = new StringBuilder(start.RawText);
            _exception = new StringBuilder(start.Exception);
        }

        public static PendingEvent From(ParsedStart start, LogParserContext context)
        {
            return new PendingEvent(start, context);
        }

        public void AppendContinuation(string line)
        {
            _rawText.AppendLine().Append(line);

            if (_exception.Length > 0)
            {
                _exception.AppendLine();
            }

            _exception.Append(line);
        }

        public LogEvent ToLogEvent(long endLineNumber)
        {
            return new LogEvent
            {
                Id = Guid.NewGuid().ToString("N"),
                ProjectId = _context.ProjectId,
                UploadSessionId = _context.UploadSessionId,
                LogFileId = _context.LogFileId,
                TimestampUtc = _start.TimestampUtc,
                TimestampMs = TimeZoneHelper.ToUnixMilliseconds(_start.TimestampUtc),
                Level = LogLevels.Normalize(_start.Level),
                Source = _start.Source,
                ThreadId = _start.ThreadId,
                LineNumber = _start.LineNumber,
                EndLineNumber = endLineNumber,
                ByteOffset = _start.ByteOffset,
                Message = _start.Message,
                Exception = _exception.ToString(),
                RawText = _rawText.ToString(),
                HttpMethod = _start.HttpMethod,
                Url = _start.Url,
                StatusCode = _start.StatusCode,
                ClientIp = _start.ClientIp,
                ServerIp = _start.ServerIp,
                UserAgent = _start.UserAgent,
                TimeTaken = _start.TimeTaken
            };
        }
    }
}
