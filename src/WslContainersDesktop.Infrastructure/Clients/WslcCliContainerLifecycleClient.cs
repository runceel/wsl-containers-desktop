using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;
using WslContainersDesktop.Infrastructure.Cli;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc</c> CLIを利用してコンテナライフサイクルポートを実装する。
/// </summary>
public sealed class WslcCliContainerLifecycleClient(IWslcCliRunner cliRunner) : IContainerLifecycleClient
{
    private readonly WslcCliCommandExecutor _executor = new(cliRunner);

    /// <inheritdoc/>
    public Task RunContainerAsync(ContainerRunRequest request, CancellationToken cancellationToken = default)
    {
        var arguments = new List<string> { "run", "-d" };
        if (request.RemoveWhenStopped)
        {
            arguments.Add("--rm");
        }

        if (request.ContainerName.Length > 0)
        {
            arguments.Add("--name");
            arguments.Add(request.ContainerName);
        }

        foreach (var mapping in request.PortMappings)
        {
            arguments.Add("-p");
            arguments.Add(mapping);
        }

        foreach (var variable in request.EnvironmentVariables)
        {
            arguments.Add("-e");
            arguments.Add(variable);
        }

        arguments.Add(request.ImageReference);

        if (request.Command.Length > 0)
        {
            arguments.Add("/bin/sh");
            arguments.Add("-lc");
            arguments.Add(request.Command);
        }

        return _executor.RunAsync(arguments, cancellationToken);
    }

    /// <inheritdoc/>
    public Task StartAsync(string containerId, CancellationToken cancellationToken = default)
    {
        return _executor.RunAsync(["container", "start", containerId], cancellationToken);
    }

    /// <inheritdoc/>
    public Task StopAsync(string containerId, CancellationToken cancellationToken = default)
    {
        return _executor.RunAsync(["container", "stop", containerId], cancellationToken);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string containerId, CancellationToken cancellationToken = default)
    {
        // -f（強制削除）を付けない。これにより実行中のコンテナに対する削除はwslc自体が
        // 拒否するため、Application層での事前検証と合わせて二重に安全性が確保される
        // （ADR-0009参照）。
        return _executor.RunAsync(["container", "remove", containerId], cancellationToken);
    }
}
