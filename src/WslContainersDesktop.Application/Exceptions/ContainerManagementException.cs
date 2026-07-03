namespace WslContainersDesktop.Application.Exceptions;

/// <summary>
/// コンテナ管理ユースケースで発生する例外の基底クラス。
/// </summary>
public abstract class ContainerManagementException : Exception
{
    /// <summary>
    /// <see cref="ContainerManagementException"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="message">エラーメッセージ。</param>
    /// <param name="innerException">内部例外（存在する場合）。</param>
    protected ContainerManagementException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
