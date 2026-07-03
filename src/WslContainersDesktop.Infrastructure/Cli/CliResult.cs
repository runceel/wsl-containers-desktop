namespace WslContainersDesktop.Infrastructure.Cli;

/// <summary>
/// CLIプロセスの実行結果。
/// </summary>
/// <param name="ExitCode">プロセスの終了コード。</param>
/// <param name="StandardOutput">標準出力の内容。</param>
/// <param name="StandardError">標準エラー出力の内容。</param>
public sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);
