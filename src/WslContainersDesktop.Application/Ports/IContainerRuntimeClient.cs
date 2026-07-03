using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Ports;

/// <summary>
/// コンテナランタイム（wslc CLI）との通信を抽象化するアウトバウンドポート。
/// Infrastructure層で実装される。
/// </summary>
public interface IContainerRuntimeClient
{
    /// <summary>
    /// 現在存在するすべてのコンテナを取得する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<IReadOnlyList<Container>> ListContainersAsync(CancellationToken cancellationToken = default);

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
