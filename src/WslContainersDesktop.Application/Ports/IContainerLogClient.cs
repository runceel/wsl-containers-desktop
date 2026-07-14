namespace WslContainersDesktop.Application.Ports;

/// <summary>
/// コンテナログ操作を抽象化するアウトバウンドポート。
/// Infrastructure層で実装される。
/// </summary>
public interface IContainerLogClient
{
    /// <summary>
    /// 指定したコンテナの既存ログを取得する。
    /// </summary>
    /// <param name="containerId">対象コンテナのID。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<IReadOnlyList<string>> GetContainerLogsAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したコンテナの新規ログを追跡する。
    /// </summary>
    /// <param name="containerId">対象コンテナのID。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    IAsyncEnumerable<string> FollowContainerLogsAsync(string containerId, CancellationToken cancellationToken = default);
}
