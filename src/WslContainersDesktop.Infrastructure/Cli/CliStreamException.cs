namespace WslContainersDesktop.Infrastructure.Cli;

/// <summary>
/// ストリーミングCLIプロセスが非ゼロ終了したことを表す例外。
/// </summary>
public sealed class CliStreamException(string command, int exitCode, string message) : Exception(message)
{
    /// <summary>
    /// 失敗したコマンド。
    /// </summary>
    public string Command { get; } = command;

    /// <summary>
    /// プロセスの終了コード。
    /// </summary>
    public int ExitCode { get; } = exitCode;
}
