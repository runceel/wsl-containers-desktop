using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;

namespace WslContainersDesktop.Infrastructure.Cli;

/// <summary>
/// <see cref="System.Diagnostics.Process"/> を利用する対話的な <c>wslc</c> プロセス。
/// </summary>
public sealed class WslcInteractiveProcess : IWslcInteractiveProcess
{
    private const int BufferSize = 1024;
    private const int OutputChannelCapacity = 5000;
    private readonly Process _process;

    public WslcInteractiveProcess(string executablePath, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        _process = new Process { StartInfo = startInfo };
    }

    public bool HasExited => _process.HasExited;

    public int ExitCode => _process.ExitCode;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _process.Start();
        _process.StandardInput.AutoFlush = true;
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<string> ReadOutputAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var readerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(OutputChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
        });
        var stdoutTask = ReadChunksAsync(_process.StandardOutput, channel.Writer, readerCancellation.Token);
        var stderrTask = ReadChunksAsync(_process.StandardError, channel.Writer, readerCancellation.Token);
        _ = CompleteAfterProcessExitAsync(channel.Writer, stdoutTask, stderrTask, readerCancellation.Token);

        try
        {
            await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return chunk;
            }
        }
        finally
        {
            await readerCancellation.CancelAsync();
            try
            {
                await Task.WhenAll(stdoutTask, stderrTask);
            }
            catch (OperationCanceledException) when (readerCancellation.IsCancellationRequested)
            {
            }
        }
    }

    public Task WriteInputAsync(string input, CancellationToken cancellationToken)
    {
        return _process.StandardInput.WriteAsync(input.AsMemory(), cancellationToken);
    }

    public Task FlushInputAsync(CancellationToken cancellationToken)
    {
        return _process.StandardInput.FlushAsync(cancellationToken);
    }

    public void Kill()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
        }
    }

    public void Dispose()
    {
        _process.Dispose();
    }

    private async Task CompleteAfterProcessExitAsync(
        ChannelWriter<string> writer,
        Task stdoutTask,
        Task stderrTask,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.WhenAll(stdoutTask, stderrTask);
            await _process.WaitForExitAsync(cancellationToken);
            writer.TryComplete();
        }
        catch (Exception ex)
        {
            writer.TryComplete(ex);
        }
    }

    private static async Task ReadChunksAsync(StreamReader reader, ChannelWriter<string> writer, CancellationToken cancellationToken)
    {
        var buffer = new char[BufferSize];
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (count == 0)
            {
                return;
            }

            await writer.WriteAsync(new string(buffer, 0, count), cancellationToken);
        }
    }
}
