using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Ports;

/// <summary>
/// コンテナーネットワーク管理ユースケースを提供するインバウンドポート。
/// </summary>
public interface INetworkManagementService
{
    /// <summary>
    /// 現在存在するすべてのコンテナーネットワークを取得する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<IReadOnlyList<ContainerNetworkResource>> GetNetworksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定した名前のコンテナーネットワークを作成する。
    /// </summary>
    /// <param name="name">作成するネットワーク名。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>作成したネットワーク。</returns>
    Task<ContainerNetworkResource> CreateAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したコンテナーネットワークを削除する。
    /// </summary>
    /// <param name="name">削除するネットワーク名。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task DeleteAsync(string name, CancellationToken cancellationToken = default);
}
