using System.Runtime.CompilerServices;
using WslContainersDesktop.Application.Ports;

namespace WslContainersDesktop.Infrastructure.Cli;

/// <summary>
/// <see cref="IWslcInteractiveProcess"/> を利用するコンテナexecセッション。
/// </summary>
public sealed class WslcContainerExecSession(IWslcInteractiveProcess process) : IContainerExecSession
{
    private bool _closed;

    public bool IsClosed => _closed || process.HasExited;

    public async IAsyncEnumerable<string> ReadOutputAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var chunk in process.ReadOutputAsync(cancellationToken))
        {
            yield return chunk;
        }

        _closed = true;
    }

    public async Task SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        await process.WriteInputAsync(command + "\n", cancellationToken);
        await process.FlushInputAsync(cancellationToken);
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_closed)
        {
            return Task.CompletedTask;
        }

        process.Kill();
        process.Dispose();
        _closed = true;
        return Task.CompletedTask;
    }
}
