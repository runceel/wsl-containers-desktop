using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Ports;

/// <summary>
/// コンテナ管理ユースケースを提供するインバウンドポート。
/// </summary>
public interface IContainerManagementService
{
    /// <summary>
    /// 現在存在するすべてのコンテナを取得する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<IReadOnlyList<Container>> GetContainersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 現在稼働中のコンテナのリソース使用量を取得する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<IReadOnlyList<ContainerResourceUsage>> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したコンテナを起動する。停止中でない場合は
    /// <see cref="Exceptions.InvalidContainerOperationException"/> をスローする。
    /// </summary>
    /// <param name="containerId">対象コンテナのID。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>起動後のコンテナ。</returns>
    Task<Container> StartAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したコンテナを停止する。実行中でない場合は
    /// <see cref="Exceptions.InvalidContainerOperationException"/> をスローする。
    /// </summary>
    /// <param name="containerId">対象コンテナのID。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>停止後のコンテナ。</returns>
    Task<Container> StopAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したコンテナを再起動する。実行中でない場合は
    /// <see cref="Exceptions.InvalidContainerOperationException"/> をスローする
    /// （停止中のコンテナに対しては「起動」にすり替えない）。
    /// </summary>
    /// <param name="containerId">対象コンテナのID。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>再起動後のコンテナ。</returns>
    Task<Container> RestartAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したコンテナを削除する。停止中でない場合は
    /// <see cref="Exceptions.InvalidContainerOperationException"/> をスローする。
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
    /// 停止中の場合は <see cref="Exceptions.InvalidContainerOperationException"/> をスローする。
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
