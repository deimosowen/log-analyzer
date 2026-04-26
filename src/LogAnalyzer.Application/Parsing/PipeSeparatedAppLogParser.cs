using System.Text.RegularExpressions;
using LogAnalyzer.Application.Time;
using LogAnalyzer.Domain;

namespace LogAnalyzer.Application.Parsing;

public sealed class PipeSeparatedAppLogParser : RegexLineLogParser
{
    private static readonly Regex Pattern = new(
        @"^(?<level>\w+)\s+\|\s+(?<thread>\d+)\s+\|\s+(?<timestamp>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}(?:\.\d+)?)\s+\|\s+(?<message>.*?)\s+\|\s*(?<exception>.*?)(?:\s+\|)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public override string Name => "Application pipe-separated";
    public override string Format => LogFormats.AppPipeLog;

    public override bool CanParse(LogSample sample)
    {
        return sample.Lines.Count(line => Pattern.IsMatch(line)) >= Math.Min(2, sample.Lines.Count);
    }

    protected override bool TryParseStart(
        string line,
        LogParserContext context,
        long lineNumber,
        long byteOffset,
        out ParsedStart parsed)
    {
        parsed = default!;
        var match = Pattern.Match(line);
        if (!match.Success ||
            !TimeZoneHelper.TryParseLogTimestamp(match.Groups["timestamp"].Value, out var timestamp))
        {
            return false;
        }

        parsed = new ParsedStart(
            ToUtc(timestamp, context),
            match.Groups["level"].Value,
            string.Empty,
            match.Groups["thread"].Value,
            lineNumber,
            byteOffset,
            match.Groups["message"].Value.Trim(),
            match.Groups["exception"].Value.Trim(),
            line);

        return true;
    }
}
