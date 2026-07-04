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
    /// 現在存在するすべてのコンテナーイメージを取得する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<IReadOnlyList<ContainerImage>> ListImagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したイメージ参照を取得する。
    /// </summary>
    /// <param name="imageReference">取得するイメージ参照。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task PullImageAsync(string imageReference, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したコンテナーイメージを削除する。
    /// </summary>
    /// <param name="imageId">対象イメージのID。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task DeleteImageAsync(string imageId, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// 指定したコンテナの既存ログを取得する。
    /// </summary>
    /// <param name="containerId">対象コンテナのID。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<IReadOnlyList<string>> GetContainerLogsAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したコンテナの詳細情報を取得する。
    /// </summary>
    /// <param name="containerId">対象コンテナのID。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<ContainerDetail> GetContainerDetailAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定した稼働中コンテナ内に対話的なexecセッションを開く。
    /// </summary>
    /// <param name="containerId">対象コンテナのID。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<IContainerExecSession> OpenExecSessionAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したコンテナの新規ログを追跡する。
    /// </summary>
    /// <param name="containerId">対象コンテナのID。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    IAsyncEnumerable<string> FollowContainerLogsAsync(string containerId, CancellationToken cancellationToken = default);
}
