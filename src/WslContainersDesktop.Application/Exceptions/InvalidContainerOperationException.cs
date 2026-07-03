namespace WslContainersDesktop.Application.Exceptions;

/// <summary>
/// コンテナの現在の状態に対して許可されない操作が要求されたことを表す例外。
/// </summary>
public sealed class InvalidContainerOperationException : ContainerManagementException
{
    /// <summary>
    /// <see cref="InvalidContainerOperationException"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="containerId">対象コンテナのID。</param>
    /// <param name="operationName">要求された操作名（例: "Start"）。</param>
    public InvalidContainerOperationException(string containerId, string operationName)
        : base($"コンテナー '{containerId}' に対して操作 '{operationName}' は現在の状態では実行できません。")
    {
        ContainerId = containerId;
        OperationName = operationName;
    }

    /// <summary>
    /// 対象コンテナのID。
    /// </summary>
    public string ContainerId { get; }

    /// <summary>
    /// 要求された操作名。
    /// </summary>
    public string OperationName { get; }
}
