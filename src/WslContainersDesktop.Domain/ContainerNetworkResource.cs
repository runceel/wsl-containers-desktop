namespace WslContainersDesktop.Domain;

/// <summary>
/// WSL Containers上のコンテナーネットワークを表すエンティティ。
/// </summary>
/// <param name="Name">ネットワーク名。</param>
/// <param name="Driver">ネットワークドライバー名。</param>
/// <param name="CreatedAt">作成日時。</param>
/// <param name="ConnectedContainerNames">このネットワークに接続しているコンテナ名の一覧。</param>
/// <param name="IsSystem">システムネットワークかどうか。</param>
public sealed record ContainerNetworkResource(
    string Name,
    string Driver,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> ConnectedContainerNames,
    bool IsSystem)
{
    /// <summary>
    /// このネットワークに接続しているコンテナ数。
    /// </summary>
    public int ConnectedContainerCount => ConnectedContainerNames.Count;

    /// <summary>
    /// ネットワークがいずれかのコンテナから使用されているかどうか。
    /// </summary>
    public bool IsInUse => ConnectedContainerCount > 0;

    /// <summary>
    /// ネットワークを削除できるかどうか。
    /// </summary>
    public bool CanDelete => !IsSystem && !IsInUse;
}
