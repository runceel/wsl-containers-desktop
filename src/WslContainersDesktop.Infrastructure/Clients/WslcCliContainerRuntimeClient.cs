using System.Text.Json;
using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;
using WslContainersDesktop.Infrastructure.Cli;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc</c> CLIをプロセスとして呼び出すことで <see cref="IContainerRuntimeClient"/> を実装する。
/// ネイティブSDK（Microsoft.WSL.Containers）はコンテナ一覧の取得APIを提供していないため、
/// CLIのstdoutをJSONとしてパースする方式を採用している（ADR-0009参照）。
/// </summary>
public sealed class WslcCliContainerRuntimeClient(IWslcCliRunner cliRunner) : IContainerRuntimeClient
{
    /// <summary>
    /// wslc SDKの <c>ContainerState.Running</c> に対応する数値。
    /// </summary>
    private const int RunningStateValue = 2;

    public async Task<IReadOnlyList<Container>> ListContainersAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["list", "-a", "--format", "json"], cancellationToken);

        List<ContainerListItemDto>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<ContainerListItemDto>>(result.StandardOutput);
        }
        catch (JsonException ex)
        {
            throw new ContainerRuntimeException(
                command: "list -a --format json",
                exitCode: result.ExitCode,
                message: "コンテナ一覧の解析に失敗しました。",
                innerException: ex);
        }

        if (items is null)
        {
            return [];
        }

        return items
            .Select(item => new Container(
                Id: item.Id,
                Name: item.Name,
                Image: item.Image,
                State: item.State == RunningStateValue ? ContainerState.Running : ContainerState.Stopped,
                CreatedAt: DateTimeOffset.FromUnixTimeSeconds(item.CreatedAt)))
            .ToList();
    }

    public Task StartAsync(string containerId, CancellationToken cancellationToken = default)
    {
        return RunAsync(["container", "start", containerId], cancellationToken);
    }

    public Task StopAsync(string containerId, CancellationToken cancellationToken = default)
    {
        return RunAsync(["container", "stop", containerId], cancellationToken);
    }

    public Task DeleteAsync(string containerId, CancellationToken cancellationToken = default)
    {
        // -f（強制削除）を付けない。これにより実行中のコンテナに対する削除はwslc自体が
        // 拒否するため、Application層での事前検証と合わせて二重に安全性が確保される
        // （ADR-0009参照）。
        return RunAsync(["container", "remove", containerId], cancellationToken);
    }

    private async Task<CliResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
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
}
