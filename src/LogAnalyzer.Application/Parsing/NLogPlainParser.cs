using System.Text.RegularExpressions;
using LogAnalyzer.Application.Time;
using LogAnalyzer.Domain;

namespace LogAnalyzer.Application.Parsing;

public sealed class NLogPlainParser : RegexLineLogParser
{
    private static readonly Regex Pattern = new(
        @"^(?<timestamp>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}(?:\.\d+)?)\s+(?<level>\w+)\s+(?:(?<source>[\w\.\-]+):\s+)?(?<message>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public override string Name => "NLog plain";
    public override string Format => LogFormats.NLogPlain;

    public override bool CanParse(LogSample sample)
    {
        return sample.Lines.Count(line => Pattern.IsMatch(line)) >=
               Math.Min(ParserDefaults.ParserDetectionMinimumMatches, sample.Lines.Count);
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
            match.Groups["source"].Value,
            string.Empty,
            lineNumber,
            byteOffset,
            match.Groups["message"].Value.Trim(),
            string.Empty,
            line);

        return true;
    }
}
