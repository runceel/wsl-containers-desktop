namespace WslContainersDesktop.Application.Exceptions;

/// <summary>
/// コンテナランタイム（wslc CLI）呼び出しが失敗したことを表す例外。
/// </summary>
public sealed class ContainerRuntimeException : ContainerManagementException
{
    /// <summary>
    /// <see cref="ContainerRuntimeException"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="command">実行したコマンド（診断用）。</param>
    /// <param name="exitCode">プロセスの終了コード。</param>
    /// <param name="message">エラーメッセージ。</param>
    /// <param name="innerException">内部例外（存在する場合）。</param>
    public ContainerRuntimeException(string command, int exitCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Command = command;
        ExitCode = exitCode;
    }

    /// <summary>
    /// 実行したコマンド（診断用）。
    /// </summary>
    public string Command { get; }

    /// <summary>
    /// プロセスの終了コード。
    /// </summary>
    public int ExitCode { get; }
}
