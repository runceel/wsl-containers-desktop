namespace WslContainersDesktop.Infrastructure.Cli;

/// <summary>
/// 外部CLIプロセスを起動する処理を抽象化する。テストで <c>wslc.exe</c> の実体に
/// 依存せず検証できるようにするための抽象。
/// </summary>
public interface IWslcCliRunner
{
    /// <summary>
    /// 指定した引数でCLIプロセスを実行し、結果を返す。
    /// </summary>
    /// <param name="arguments">コマンドライン引数（シェル解釈を経ないargvとして渡される）。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<CliResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定した引数でCLIプロセスを実行し、標準出力・標準エラーの行を逐次返す。
    /// </summary>
    /// <param name="arguments">コマンドライン引数（シェル解釈を経ないargvとして渡される）。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    IAsyncEnumerable<string> StreamLinesAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
}
