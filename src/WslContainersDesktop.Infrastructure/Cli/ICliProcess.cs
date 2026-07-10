using System.Diagnostics;

namespace WslContainersDesktop.Infrastructure.Cli;

/// <summary>
/// 外部CLIプロセスの実行をテスト可能にするための低レベル抽象。
/// </summary>
public interface ICliProcess : IDisposable
{
    /// <summary>
    /// プロセスの終了コードを取得する。
    /// </summary>
    int ExitCode { get; }

    /// <summary>
    /// プロセスが終了したかどうかを取得する。
    /// </summary>
    bool HasExited { get; }

    /// <summary>
    /// プロセスを開始する。
    /// </summary>
    void Start();

    /// <summary>
    /// 標準出力の全内容を非同期に読み取る。
    /// </summary>
    /// <param name="cancellationToken">読み取りを中止するトークン。</param>
    Task<string> ReadStandardOutputAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 標準エラー出力の全内容を非同期に読み取る。
    /// </summary>
    /// <param name="cancellationToken">読み取りを中止するトークン。</param>
    Task<string> ReadStandardErrorAsync(CancellationToken cancellationToken);

    /// <summary>
    /// プロセスの終了を待機する。
    /// </summary>
    /// <param name="cancellationToken">待機を中止するトークン。</param>
    Task WaitForExitAsync(CancellationToken cancellationToken);

    /// <summary>
    /// プロセスツリー全体を終了する。
    /// </summary>
    void KillEntireProcessTree();
}
