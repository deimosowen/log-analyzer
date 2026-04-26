namespace LogAnalyzer.Application.Parsing;

public sealed class LogParserSelector
{
    private readonly IReadOnlyList<ILogParser> _parsers;

    public LogParserSelector(IEnumerable<ILogParser> parsers)
    {
        _parsers = parsers.ToArray();
    }

    public ILogParser Select(LogSample sample)
    {
        return _parsers.First(parser => parser.CanParse(sample));
    }
}
