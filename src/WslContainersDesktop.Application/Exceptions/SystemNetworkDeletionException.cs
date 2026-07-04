namespace WslContainersDesktop.Application.Exceptions;

/// <summary>
/// システムネットワークに対して削除が要求されたことを表す例外。
/// </summary>
public sealed class SystemNetworkDeletionException : ContainerManagementException
{
    /// <summary>
    /// <see cref="SystemNetworkDeletionException"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="networkName">削除対象のネットワーク名。</param>
    public SystemNetworkDeletionException(string networkName)
        : base($"Network '{networkName}' is a system network and cannot be deleted.")
    {
        NetworkName = networkName;
    }

    /// <summary>
    /// 削除対象のネットワーク名。
    /// </summary>
    public string NetworkName { get; }
}
