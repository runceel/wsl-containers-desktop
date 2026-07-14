using System.Runtime.CompilerServices;
using System.Text.Json;
using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Infrastructure.Cli;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc</c> CLI呼び出しに共通する下位機構（プロセス実行・終了コード/ストリーム例外の
/// <see cref="ContainerRuntimeException"/> への変換・JSON配列のデシリアライズ・対話的
/// セッションのオープン）を集約する。リソース固有のDTOマッピング・詳細/統計のパース・
/// ボリューム/ネットワークの業務的なマッピングは扱わない（ADR-0017参照）。
/// </summary>
internal sealed class WslcCliCommandExecutor(IWslcCliRunner cliRunner)
{
    /// <summary>
    /// 指定した引数でCLIを一度だけ実行する。非ゼロ終了コードの場合は
    /// <see cref="ContainerRuntimeException"/> をスローする。
    /// </summary>
    /// <param name="arguments">コマンドライン引数。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    public async Task<CliResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var result = await cliRunner.RunAsync(arguments, cancellationToken);
        if (result.ExitCode != 0)
        {
            var command = string.Join(' ', arguments);
            var message = string.IsNullOrWhiteSpace(result.StandardError)
                ? $"コマンド '{command}' がエラーコード {result.ExitCode} で終了しました。"
                : result.StandardError.Trim();
            throw new ContainerRuntimeException(command, result.ExitCode, message);
        }

        return result;
    }

    /// <summary>
    /// CLI実行結果の標準出力をJSON配列としてデシリアライズする。
    /// 解析に失敗した場合は <see cref="ContainerRuntimeException"/> をスローする。
    /// </summary>
    /// <typeparam name="TDto">配列要素のDTO型。</typeparam>
    /// <param name="result">CLI実行結果。</param>
    /// <param name="command">失敗時に例外へ含めるコマンド文字列。</param>
    /// <param name="failureMessage">解析失敗時のメッセージ。</param>
    public static List<TDto>? DeserializeJsonList<TDto>(CliResult result, string command, string failureMessage)
    {
        try
        {
            return JsonSerializer.Deserialize<List<TDto>>(result.StandardOutput);
        }
        catch (JsonException ex)
        {
            throw new ContainerRuntimeException(
                command: command,
                exitCode: result.ExitCode,
                message: failureMessage,
                innerException: ex);
        }
    }

    /// <summary>
    /// 指定した引数でCLIを実行し、標準出力・標準エラーの行を逐次返す。
    /// ストリームが非ゼロ終了コードで終了した場合、<see cref="CliStreamException"/> を
    /// <see cref="ContainerRuntimeException"/> に変換してスローする。
    /// </summary>
    /// <param name="arguments">コマンドライン引数。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    public async IAsyncEnumerable<string> StreamLinesAsync(
        IReadOnlyList<string> arguments,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var enumerator = cliRunner.StreamLinesAsync(arguments, cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = await enumerator.MoveNextAsync();
            }
            catch (CliStreamException ex)
            {
                throw new ContainerRuntimeException(ex.Command, ex.ExitCode, ex.Message, ex);
            }

            if (!hasNext)
            {
                yield break;
            }

            yield return enumerator.Current.TrimEnd('\r');
        }
    }

    /// <summary>
    /// 指定した引数で対話的なCLIプロセスを起動し、セッションを返す。
    /// </summary>
    /// <param name="arguments">コマンドライン引数。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    public Task<IContainerExecSession> OpenInteractiveAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        return cliRunner.OpenInteractiveAsync(arguments, cancellationToken);
    }
}
