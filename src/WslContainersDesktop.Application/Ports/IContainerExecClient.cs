namespace WslContainersDesktop.Application.Ports;

/// <summary>
/// コンテナexec操作を抽象化するアウトバウンドポート。
/// Infrastructure層で実装される。
/// </summary>
public interface IContainerExecClient
{
    /// <summary>
    /// 指定した稼働中コンテナ内に対話的なexecセッションを開く。
    /// </summary>
    /// <param name="containerId">対象コンテナのID。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<IContainerExecSession> OpenExecSessionAsync(string containerId, CancellationToken cancellationToken = default);
}
