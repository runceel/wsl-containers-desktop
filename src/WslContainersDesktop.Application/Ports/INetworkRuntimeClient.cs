using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Ports;

/// <summary>
/// ネットワークリソース操作を抽象化するアウトバウンドポート。
/// Infrastructure層で実装される。
/// </summary>
public interface INetworkRuntimeClient
{
    /// <summary>
    /// 現在存在するすべてのコンテナーネットワークを取得する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<IReadOnlyList<ContainerNetworkResource>> ListNetworksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定した名前のコンテナーネットワークを作成する。
    /// </summary>
    /// <param name="name">作成するネットワーク名。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task CreateNetworkAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したコンテナーネットワークを削除する。
    /// </summary>
    /// <param name="name">削除するネットワーク名。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task DeleteNetworkAsync(string name, CancellationToken cancellationToken = default);
}
