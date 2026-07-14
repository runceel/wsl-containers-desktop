using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Ports;

/// <summary>
/// コンテナのライフサイクル操作を抽象化するアウトバウンドポート。
/// Infrastructure層で実装される。
/// </summary>
public interface IContainerLifecycleClient
{
    /// <summary>
    /// 指定したイメージから新しいコンテナーを起動する。
    /// </summary>
    /// <param name="request">起動要求。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task RunContainerAsync(ContainerRunRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したコンテナを起動する。
    /// </summary>
    /// <param name="containerId">対象コンテナのID。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task StartAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したコンテナを停止する。
    /// </summary>
    /// <param name="containerId">対象コンテナのID。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task StopAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したコンテナを削除する。
    /// </summary>
    /// <param name="containerId">対象コンテナのID。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task DeleteAsync(string containerId, CancellationToken cancellationToken = default);
}
