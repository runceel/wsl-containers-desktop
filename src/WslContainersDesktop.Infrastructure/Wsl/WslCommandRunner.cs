using System.Diagnostics;
using System.Text;
using WslContainersDesktop.Infrastructure.Cli;

namespace WslContainersDesktop.Infrastructure.Wsl;

/// <summary>
/// <c>wsl.exe</c> をプロセスとして起動する <see cref="IWslCommandRunner"/> の実装。
/// ロジックを持たない薄いプロセス起動アダプターであり、単体テストの対象外とする。
/// </summary>
public sealed class WslCommandRunner : IWslCommandRunner
{
    private readonly string _executablePath;
    private readonly CliProcessExecutor _cliProcessExecutor;

    /// <summary>
    /// <see cref="WslCommandRunner"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="executablePath">実行対象の <c>wsl.exe</c> パス（既定は <c>"wsl.exe"</c>）。</param>
    public WslCommandRunner(string executablePath = "wsl.exe")
    {
        _executablePath = executablePath;
        _cliProcessExecutor = new CliProcessExecutor();
    }

    /// <inheritdoc />
    public async Task<CliResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _executablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            // wsl.exe は --version などの出力をUTF-16LEで返すため、明示的に指定する。
            StandardOutputEncoding = Encoding.Unicode,
            StandardErrorEncoding = Encoding.Unicode,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return await _cliProcessExecutor.ExecuteAsync(startInfo, cancellationToken);
    }
}
