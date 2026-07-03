namespace WslContainersDesktop.Application.Exceptions;

/// <summary>
/// 指定されたIDのコンテナが見つからなかったことを表す例外。
/// </summary>
public sealed class ContainerNotFoundException : ContainerManagementException
{
    /// <summary>
    /// <see cref="ContainerNotFoundException"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="containerId">見つからなかったコンテナのID。</param>
    public ContainerNotFoundException(string containerId)
        : base($"コンテナー '{containerId}' が見つかりませんでした。")
    {
        ContainerId = containerId;
    }

    /// <summary>
    /// 見つからなかったコンテナのID。
    /// </summary>
    public string ContainerId { get; }
}
