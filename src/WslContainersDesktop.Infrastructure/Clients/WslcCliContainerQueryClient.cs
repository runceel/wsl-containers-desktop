using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;
using WslContainersDesktop.Infrastructure.Cli;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc</c> CLIを利用してコンテナ問い合わせポートを実装する。
/// </summary>
public sealed class WslcCliContainerQueryClient(IWslcCliRunner cliRunner) : IContainerQueryClient
{
    private static readonly string[] ListContainersCommand = ["list", "-a", "--format", "json", "--no-trunc"];

    private const string ListContainersCommandText = "list -a --format json --no-trunc";

    /// <summary>
    /// wslc SDKの <c>ContainerState.Running</c> に対応する数値。
    /// </summary>
    private const int RunningStateValue = 2;

    private readonly WslcCliCommandExecutor _executor = new(cliRunner);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Container>> ListContainersAsync(CancellationToken cancellationToken = default)
    {
        var result = await _executor.RunAsync(ListContainersCommand, cancellationToken);

        var items = WslcCliCommandExecutor.DeserializeJsonList<ContainerListItemDto>(
            result,
            command: ListContainersCommandText,
            failureMessage: "コンテナ一覧の解析に失敗しました。",
            allowEmptyOutput: true,
            allowJsonLines: true);
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
                CreatedAt: item.CreatedAt))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<ContainerDetail> GetContainerDetailAsync(string containerId, CancellationToken cancellationToken = default)
    {
        var result = await _executor.RunAsync(["container", "inspect", containerId], cancellationToken);

        var items = WslcCliCommandExecutor.DeserializeJsonList<ContainerInspectDto>(
            result,
            command: $"container inspect {containerId}",
            failureMessage: "コンテナ詳細情報の解析に失敗しました。");
        var item = items?.FirstOrDefault();
        if (item is null)
        {
            throw new ContainerRuntimeException($"container inspect {containerId}", result.ExitCode, "コンテナ詳細情報が見つかりませんでした。");
        }

        return ContainerDetailMapper.MapDetail(item);
    }
}
