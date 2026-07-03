using System.Diagnostics;
using System.Text;

namespace WslContainersDesktop.Infrastructure.Cli;

/// <summary>
/// <c>wslc</c> CLI（既定）または任意の実行可能ファイルをプロセスとして起動する
/// <see cref="IWslcCliRunner"/> の実装。
/// </summary>
public sealed class WslcCliRunner(string executablePath = "wslc") : IWslcCliRunner
{
    /// <summary>
    /// 実行対象の実行可能ファイルパス（既定は <c>"wslc"</c>）。
    /// </summary>
    public string ExecutablePath { get; } = executablePath;

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
}
