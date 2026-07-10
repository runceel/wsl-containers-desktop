using System.Diagnostics;

namespace WslContainersDesktop.Infrastructure.Cli;

/// <summary>
/// 標準出力と標準エラーを収集しながら、外部CLIプロセスを実行する。
/// </summary>
public sealed class CliProcessExecutor
{
    private readonly Func<ProcessStartInfo, ICliProcess> _processFactory;

    /// <summary>
    /// <see cref="CliProcessExecutor"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="processFactory">CLIプロセスを生成するファクトリ。省略時は <see cref="Process"/> を使用する。</param>
    public CliProcessExecutor(Func<ProcessStartInfo, ICliProcess>? processFactory = null)
    {
        _processFactory = processFactory ?? (startInfo => new CliProcess(startInfo));
    }

    /// <summary>
    /// プロセスを実行し、終了コードと収集した標準出力・標準エラーを返す。
    /// キャンセル時にはプロセスツリーを終了し、終了と出力読み取りの完了後に破棄する。
    /// </summary>
    /// <param name="startInfo">実行するプロセスの開始情報。</param>
    /// <param name="cancellationToken">実行を中止するトークン。</param>
    /// <returns>CLIの終了コードと出力。</returns>
    public async Task<CliResult> ExecuteAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default)
    {
        var process = _processFactory(startInfo);

        try
        {
            process.Start();

            var standardOutputTask = process.ReadStandardOutputAsync(cancellationToken);
            var standardErrorTask = process.ReadStandardErrorAsync(cancellationToken);

            try
            {
                await process.WaitForExitAsync(cancellationToken);
                await Task.WhenAll(standardOutputTask, standardErrorTask);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                try
                {
                    process.KillEntireProcessTree();
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                    // The process ended between the exit check and Kill.
                }

                await process.WaitForExitAsync(CancellationToken.None);
                await AwaitCancellationCompletionAsync(standardOutputTask, standardErrorTask, cancellationToken);
                throw new OperationCanceledException(cancellationToken);
            }

            var standardOutput = await standardOutputTask;
            var standardError = await standardErrorTask;
            return new CliResult(process.ExitCode, standardOutput, standardError);
        }
        finally
        {
            process.Dispose();
        }
    }

    private static async Task AwaitCancellationCompletionAsync(
        Task<string> standardOutputTask,
        Task<string> standardErrorTask,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.WhenAll(standardOutputTask, standardErrorTask);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
