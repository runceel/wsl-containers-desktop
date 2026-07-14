using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;
using WslContainersDesktop.Infrastructure.Cli;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc</c> CLIを利用してネットワークリソースポートを実装する。
/// </summary>
public sealed class WslcCliNetworkRuntimeClient(IWslcCliRunner cliRunner) : INetworkRuntimeClient
{
    /// ネットワーク検査時に同時に実行する最大並列数。
    private const int InspectionConcurrencyLimit = 4;

    private readonly WslcCliCommandExecutor _executor = new(cliRunner);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ContainerNetworkResource>> ListNetworksAsync(CancellationToken cancellationToken = default)
    {
        var result = await _executor.RunAsync(["network", "list", "--format", "json"], cancellationToken);

        var items = WslcCliCommandExecutor.DeserializeJsonList<NetworkListItemDto>(
            result,
            command: "network list --format json",
            failureMessage: "コンテナーネットワーク一覧の解析に失敗しました。");
        if (items is null)
        {
            return [];
        }

        return await BoundedConcurrencyInspector.InspectAsync(
            items,
            InspectNetworkAsync,
            InspectionConcurrencyLimit,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task CreateNetworkAsync(string name, CancellationToken cancellationToken = default)
    {
        return _executor.RunAsync(["network", "create", name], cancellationToken);
    }

    /// <inheritdoc/>
    public Task DeleteNetworkAsync(string name, CancellationToken cancellationToken = default)
    {
        return _executor.RunAsync(["network", "remove", name], cancellationToken);
    }

    private async Task<ContainerNetworkResource> InspectNetworkAsync(NetworkListItemDto listItem, CancellationToken cancellationToken)
    {
        var result = await _executor.RunAsync(["network", "inspect", listItem.Name], cancellationToken);

        var items = WslcCliCommandExecutor.DeserializeJsonList<NetworkInspectDto>(
            result,
            command: $"network inspect {listItem.Name}",
            failureMessage: "コンテナーネットワーク詳細情報の解析に失敗しました。");
        var item = items?.FirstOrDefault();
        if (item is null)
        {
            return new ContainerNetworkResource(listItem.Name, listItem.Driver, DateTimeOffset.MinValue, [], listItem.IsSystem);
        }

        return new ContainerNetworkResource(
            Name: string.IsNullOrEmpty(item.Name) ? listItem.Name : item.Name,
            Driver: string.IsNullOrEmpty(item.Driver) ? listItem.Driver : item.Driver,
            CreatedAt: CliDateTimeParsing.ParseDateTimeOffsetOrDefault(item.CreatedAt),
            ConnectedContainerNames: [],
            IsSystem: listItem.IsSystem || item.IsSystem);
    }
}
