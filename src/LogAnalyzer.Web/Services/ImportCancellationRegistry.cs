using System.Collections.Concurrent;

namespace LogAnalyzer.Web.Services;

public sealed class ImportCancellationRegistry
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _running = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string uploadSessionId, CancellationTokenSource cancellationTokenSource)
    {
        _running[uploadSessionId] = cancellationTokenSource;
    }

    public void Unregister(string uploadSessionId)
    {
        _running.TryRemove(uploadSessionId, out _);
    }

    public bool Cancel(string uploadSessionId)
    {
        if (!_running.TryGetValue(uploadSessionId, out var source))
        {
            return false;
        }

        source.Cancel();
        return true;
    }
}
