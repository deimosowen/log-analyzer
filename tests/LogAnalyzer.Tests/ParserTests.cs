using System.Text;
using LogAnalyzer.Application;
using LogAnalyzer.Application.Parsing;
using LogAnalyzer.Domain;

namespace LogAnalyzer.Tests;

public sealed class ParserTests
{
    private static readonly LogParserContext Context = new(
        Guid.NewGuid().ToString("N"),
        Guid.NewGuid().ToString("N"),
        Guid.NewGuid().ToString("N"),
        "UTC",
        true,
        true);

    [Fact]
    public async Task PipeParserCombinesMultilineException()
    {
        var text = """
            WARN | 247 | 2026-04-21 05:45:29.6611 | Message text | Exception text |
               at Demo.Service.Run()
            INFO | 248 | 2026-04-21 05:45:30.0000 | Done | |
            """;

        var events = await ParseAsync(new PipeSeparatedAppLogParser(), text);

        Assert.Equal(2, events.Count);
        Assert.Equal(LogFormats.AppPipeLog, new PipeSeparatedAppLogParser().Format);
        Assert.Equal("WARN", events[0].Level);
        Assert.Equal("247", events[0].ThreadId);
        Assert.Contains("Demo.Service.Run", events[0].Exception);
        Assert.Equal(2, events[0].EndLineNumber);
    }

    [Fact]
    public async Task NLogParserReadsSourceAndLevel()
    {
        var text = "2026-02-18 14:22:33.8035 Warn Demo.Logger: Message text";

        var events = await ParseAsync(new NLogPlainParser(), text);

        Assert.Single(events);
        Assert.Equal("WARN", events[0].Level);
        Assert.Equal("Demo.Logger", events[0].Source);
        Assert.Equal("Message text", events[0].Message);
    }

    [Fact]
    public async Task IisParserUsesFieldsOrder()
    {
        var text = """
            #Fields: date time s-ip c-ip cs-method cs-uri-stem sc-status time-taken cs(User-Agent)
            2026-04-21 05:45:29 10.0.0.1 10.0.0.2 GET /api/test 500 123 TestAgent
            """;

        var events = await ParseAsync(new IisW3CParser(), text);

        Assert.Single(events);
        Assert.Equal("IIS", events[0].Source);
        Assert.Equal("ERROR", events[0].Level);
        Assert.Equal("GET", events[0].HttpMethod);
        Assert.Equal("/api/test", events[0].Url);
        Assert.Equal(500, events[0].StatusCode);
        Assert.Equal("10.0.0.2", events[0].ClientIp);
    }

    private static async Task<List<LogEvent>> ParseAsync(ILogParser parser, string text)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var events = new List<LogEvent>();
        await foreach (var item in parser.ParseAsync(stream, Context, CancellationToken.None))
        {
            events.Add(item);
        }

        return events;
    }

}
