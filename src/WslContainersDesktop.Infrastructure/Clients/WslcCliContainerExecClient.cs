using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Infrastructure.Cli;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc</c> CLIを利用してコンテナexecポートを実装する。
/// </summary>
public sealed class WslcCliContainerExecClient(IWslcCliRunner cliRunner) : IContainerExecClient
{
    private readonly WslcCliCommandExecutor _executor = new(cliRunner);

    /// <inheritdoc/>
    public Task<IContainerExecSession> OpenExecSessionAsync(string containerId, CancellationToken cancellationToken = default)
    {
        return _executor.OpenInteractiveAsync(["container", "exec", "-i", containerId, "/bin/sh"], cancellationToken);
    }
}
