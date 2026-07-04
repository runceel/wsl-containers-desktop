namespace WslContainersDesktop.Infrastructure.Cli;

/// <summary>
/// stdin/stdout/stderrを接続した対話的な <c>wslc</c> プロセス抽象。
/// </summary>
public interface IWslcInteractiveProcess : IDisposable
{
    /// <summary>
    /// プロセスが終了しているかどうか。
    /// </summary>
    bool HasExited { get; }

    /// <summary>
    /// プロセスの終了コード。未終了の場合は未定義。
    /// </summary>
    int ExitCode { get; }

    /// <summary>
    /// プロセスを起動する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 標準出力・標準エラーをチャンク単位で読み取る。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    IAsyncEnumerable<string> ReadOutputAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 標準入力へ文字列を書き込む。
    /// </summary>
    /// <param name="input">書き込む文字列。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task WriteInputAsync(string input, CancellationToken cancellationToken);

    /// <summary>
    /// 標準入力をフラッシュする。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task FlushInputAsync(CancellationToken cancellationToken);

    /// <summary>
    /// プロセスを終了する。
    /// </summary>
    void Kill();
}
