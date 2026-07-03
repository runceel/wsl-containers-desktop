namespace WslContainersDesktop.Domain;

/// <summary>
/// コンテナの状態を表す。
/// </summary>
public enum ContainerState
{
    /// <summary>
    /// 停止中（未起動を含む）。
    /// </summary>
    Stopped,

    /// <summary>
    /// 実行中。
    /// </summary>
    Running,
}
