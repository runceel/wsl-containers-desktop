using System.Text.RegularExpressions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Infrastructure.Wsl;

/// <summary>
/// <c>wsl --version</c> の出力と <c>wslc.exe</c> の有無からWSL環境の生の事実を観測する
/// <see cref="IWslEnvironmentProbe"/> の実装。要件充足の判定は行わない（Application層の責務）。
/// 状態確認用途のため、コマンドの失敗や解析不能は例外とせずバージョン <see langword="null"/> として扱う。
/// </summary>
public sealed class WslEnvironmentProbe(IWslCommandRunner commandRunner, IWslcExecutableProbe wslcExecutableProbe)
    : IWslEnvironmentProbe
{
    private static readonly Regex VersionRegex = new(@"\d+\.\d+\.\d+(?:\.\d+)?", RegexOptions.Compiled);

    /// <inheritdoc />
    public async Task<WslEnvironmentInfo> GetEnvironmentInfoAsync(CancellationToken cancellationToken = default)
    {
        var version = await TryGetWslVersionAsync(cancellationToken);
        return new WslEnvironmentInfo(version, wslcExecutableProbe.IsAvailable());
    }

    private async Task<string?> TryGetWslVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await commandRunner.RunAsync(["--version"], cancellationToken);
            if (result.ExitCode != 0)
            {
                return null;
            }

            var match = VersionRegex.Match(result.StandardOutput);
            return match.Success ? match.Value : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // WSLが未インストールなどで起動に失敗した場合でも、状態確認は継続できるようにする。
            return null;
        }
    }
}
