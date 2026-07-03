using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;

namespace WslContainersDesktop.Infrastructure.Cli;

/// <summary>
/// <see cref="System.Diagnostics.Process"/> を利用する <see cref="IWslcProcess"/>。
/// </summary>
public sealed class WslcProcess : IWslcProcess
{
    private readonly Process _process;
    private readonly string _command;
    private readonly StringBuilder _standardError = new();

    public WslcProcess(string executablePath, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        _command = string.Join(' ', arguments);
        _process = new Process { StartInfo = startInfo };
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _process.Start();
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<string> ReadLinesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<string>();
        var stdoutTask = ReadStreamAsync(_process.StandardOutput, channel.Writer, captureError: false, cancellationToken);
        var stderrTask = ReadStreamAsync(_process.StandardError, channel.Writer, captureError: true, cancellationToken);

        _ = CompleteAfterProcessExitAsync(channel.Writer, stdoutTask, stderrTask, cancellationToken);

        await foreach (var line in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return line;
        }
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
            if (_process.ExitCode != 0)
            {
                var message = _standardError.Length == 0
                    ? $"コマンド '{_command}' がエラーコード {_process.ExitCode} で終了しました。"
                    : _standardError.ToString().Trim();
                writer.TryComplete(new CliStreamException(_command, _process.ExitCode, message));
                return;
            }

            writer.TryComplete();
        }
        catch (Exception ex)
        {
            writer.TryComplete(ex);
        }
    }

    private async Task ReadStreamAsync(
        StreamReader reader,
        ChannelWriter<string> writer,
        bool captureError,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                return;
            }

            if (captureError)
            {
                _standardError.AppendLine(line);
            }

            await writer.WriteAsync(line, cancellationToken);
        }
    }
}
