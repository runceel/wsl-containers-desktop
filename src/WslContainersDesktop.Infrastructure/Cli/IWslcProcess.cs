namespace WslContainersDesktop.Infrastructure.Cli;

/// <summary>
/// <c>wslc</c> プロセス起動をテスト可能にするための抽象。
/// </summary>
public interface IWslcProcess : IDisposable
{
    /// <summary>
    /// プロセスを起動する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// プロセスの標準出力・標準エラーを行単位で読み取る。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    IAsyncEnumerable<string> ReadLinesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// プロセスを終了する。
    /// </summary>
    void Kill();
}
