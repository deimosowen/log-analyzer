using System.Threading.Channels;

namespace LogAnalyzer.Application;

public sealed class ImportJobQueue : IImportJobQueue
{
    private readonly Channel<ImportJob> _channel = Channel.CreateUnbounded<ImportJob>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(ImportJob job, CancellationToken cancellationToken)
    {
        return _channel.Writer.WriteAsync(job, cancellationToken);
    }

    public ValueTask<ImportJob> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}
