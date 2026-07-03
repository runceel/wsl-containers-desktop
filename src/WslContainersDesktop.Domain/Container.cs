namespace WslContainersDesktop.Domain;

/// <summary>
/// WSL Containers上のコンテナを表すエンティティ。
/// </summary>
/// <param name="Id">コンテナID。</param>
/// <param name="Name">コンテナ名。</param>
/// <param name="Image">使用イメージ名。</param>
/// <param name="State">現在の状態。</param>
/// <param name="CreatedAt">作成日時。</param>
public sealed record Container(string Id, string Name, string Image, ContainerState State, DateTimeOffset CreatedAt)
{
    /// <summary>
    /// 「起動」操作を適用できるかどうか。
    /// </summary>
    public bool CanStart => State == ContainerState.Stopped;

    /// <summary>
    /// 「停止」操作を適用できるかどうか。
    /// </summary>
    public bool CanStop => State == ContainerState.Running;

    /// <summary>
    /// 「再起動」操作を適用できるかどうか。
    /// </summary>
    public bool CanRestart => State == ContainerState.Running;

    /// <summary>
    /// 「削除」操作を適用できるかどうか（停止中のコンテナのみ削除可能）。
    /// </summary>
    public bool CanDelete => State == ContainerState.Stopped;
}
