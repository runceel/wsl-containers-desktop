namespace WslContainersDesktop.Application.Exceptions;

/// <summary>
/// 接続中のコンテナーが存在するネットワークに対して削除が要求されたことを表す例外。
/// </summary>
public sealed class NetworkInUseException : ContainerManagementException
{
    /// <summary>
    /// <see cref="NetworkInUseException"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="networkName">削除対象のネットワーク名。</param>
    /// <param name="connectedContainerNames">対象ネットワークに接続しているコンテナ名。</param>
    public NetworkInUseException(string networkName, IReadOnlyList<string> connectedContainerNames)
        : base($"Network '{networkName}' is in use by: {string.Join(", ", connectedContainerNames)}")
    {
        NetworkName = networkName;
        ConnectedContainerNames = connectedContainerNames;
    }

    /// <summary>
    /// 削除対象のネットワーク名。
    /// </summary>
    public string NetworkName { get; }

    /// <summary>
    /// 対象ネットワークに接続しているコンテナ名。
    /// </summary>
    public IReadOnlyList<string> ConnectedContainerNames { get; }
}
