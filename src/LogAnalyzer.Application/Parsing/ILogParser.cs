using LogAnalyzer.Domain;

namespace LogAnalyzer.Application.Parsing;

public interface ILogParser
{
    string Name { get; }
    string Format { get; }
    bool CanParse(LogSample sample);

    IAsyncEnumerable<LogEvent> ParseAsync(
        Stream stream,
        LogParserContext context,
        CancellationToken cancellationToken);
}
