using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Ports;

/// <summary>
/// コンテナの問い合わせ操作を抽象化するアウトバウンドポート。
/// Infrastructure層で実装される。
/// </summary>
public interface IContainerQueryClient
{
    /// <summary>
    /// 現在存在するすべてのコンテナを取得する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<IReadOnlyList<Container>> ListContainersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したコンテナの詳細情報を取得する。
    /// </summary>
    /// <param name="containerId">対象コンテナのID。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<ContainerDetail> GetContainerDetailAsync(string containerId, CancellationToken cancellationToken = default);
}
