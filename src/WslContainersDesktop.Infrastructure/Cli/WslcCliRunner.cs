using System.Diagnostics;
using System.Text;
using WslContainersDesktop.Application.Ports;

namespace WslContainersDesktop.Infrastructure.Cli;

/// <summary>
/// <c>wslc</c> CLI（既定）または任意の実行可能ファイルをプロセスとして起動する
/// <see cref="IWslcCliRunner"/> の実装。
/// </summary>
public sealed class WslcCliRunner : IWslcCliRunner
{
    private readonly IWslcProcessFactory _processFactory;

    public WslcCliRunner(string executablePath = "wslc")
        : this(new WslcProcessFactory(), executablePath)
    {
    }

    public WslcCliRunner(IWslcProcessFactory processFactory, string executablePath = "wslc")
    {
        _processFactory = processFactory;
        ExecutablePath = executablePath;
    }

    /// <summary>
    /// 実行対象の実行可能ファイルパス（既定は <c>"wslc"</c>）。
    /// </summary>
    public string ExecutablePath { get; }

    public async Task<CliResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ExecutablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            // wslcは日本語メッセージをUTF-8で出力するため、標準出力/標準エラーの
            // デコードにもUTF-8を明示的に使用する。
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        // シェル解釈を経由せず、各引数をargvとしてそのまま渡すことで
        // スペースや特殊文字を含む値でもコマンドインジェクションの懸念なく渡せる。
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;

        return new CliResult(process.ExitCode, standardOutput, standardError);
    }

    public async IAsyncEnumerable<string> StreamLinesAsync(
        IReadOnlyList<string> arguments,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var process = _processFactory.Create(ExecutablePath, arguments);
        await process.StartAsync(cancellationToken);

        try
        {
            await foreach (var line in process.ReadLinesAsync(cancellationToken))
            {
                yield return line;
            }
        }
        finally
        {
            process.Kill();
            process.Dispose();
        }
    }

    public async Task<IContainerExecSession> OpenInteractiveAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        var process = _processFactory.CreateInteractive(ExecutablePath, arguments);
        await process.StartAsync(cancellationToken);
        return new WslcContainerExecSession(process);
    }
}
