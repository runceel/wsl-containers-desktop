using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Infrastructure.Cli;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc</c> CLIを利用してコンテナログポートを実装する。
/// </summary>
public sealed class WslcCliContainerLogClient(IWslcCliRunner cliRunner) : IContainerLogClient
{
    /// <summary>
    /// 履歴ログとして保持する最大行数。
    /// </summary>
    private const int MaxHistoricalLogLines = 5000;

    private readonly WslcCliCommandExecutor _executor = new(cliRunner);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetContainerLogsAsync(string containerId, CancellationToken cancellationToken = default)
    {
        // 実行は一度きり: `container logs` はストリーミングAPIのみを呼び出す
        // （stdout/stderrの両方が同じストリームから得られ、非ゼロ終了コードは
        // ContainerRuntimeExceptionとして表面化するため、事前のRunAsync呼び出しは不要）。
        IReadOnlyList<string> arguments = ["container", "logs", containerId];
        var lines = new Queue<string>(MaxHistoricalLogLines);

        await foreach (var line in _executor.StreamLinesAsync(arguments, cancellationToken))
        {
            if (lines.Count >= MaxHistoricalLogLines)
            {
                lines.Dequeue();
            }

            lines.Enqueue(line);
        }

        return lines.ToList();
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<string> FollowContainerLogsAsync(string containerId, CancellationToken cancellationToken = default)
    {
        var since = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        IReadOnlyList<string> arguments = ["container", "logs", "--since", since, "--follow", containerId];
        return _executor.StreamLinesAsync(arguments, cancellationToken);
    }
}
