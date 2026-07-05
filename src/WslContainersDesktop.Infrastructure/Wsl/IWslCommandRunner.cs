using WslContainersDesktop.Infrastructure.Cli;

namespace WslContainersDesktop.Infrastructure.Wsl;

/// <summary>
/// <c>wsl.exe</c> をプロセスとして起動する処理を抽象化する低レベルseam。
/// テストで実体に依存せず <see cref="WslEnvironmentProbe"/> のロジックを検証できるようにする。
/// </summary>
public interface IWslCommandRunner
{
    /// <summary>
    /// 指定した引数で <c>wsl.exe</c> を実行し、結果を返す。
    /// </summary>
    /// <param name="arguments">コマンドライン引数（シェル解釈を経ないargvとして渡される）。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<CliResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
}
