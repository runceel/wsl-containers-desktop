using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;
using WslContainersDesktop.Infrastructure.Cli;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc</c> CLIを利用してネットワークリソースポートを実装する。
/// </summary>
public sealed class WslcCliNetworkRuntimeClient(IWslcCliRunner cliRunner) : INetworkRuntimeClient
{
    private static readonly string[] ListNetworksCommand = ["network", "list", "--format", "json"];

    private const string ListNetworksCommandText = "network list --format json";

    /// ネットワーク検査時に同時に実行する最大並列数。
    private const int InspectionConcurrencyLimit = 4;

    private readonly WslcCliCommandExecutor _executor = new(cliRunner);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ContainerNetworkResource>> ListNetworksAsync(CancellationToken cancellationToken = default)
    {
        var result = await _executor.RunAsync(ListNetworksCommand, cancellationToken);

        var items = WslcCliCommandExecutor.DeserializeJsonList<NetworkListItemDto>(
            result,
            command: ListNetworksCommandText,
            failureMessage: "コンテナーネットワーク一覧の解析に失敗しました。",
            allowEmptyOutput: true,
            allowJsonLines: true);
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
        return MapNetwork(listItem, items?.FirstOrDefault());
    }

    private static ContainerNetworkResource MapNetwork(NetworkListItemDto listItem, NetworkInspectDto? inspectItem)
    {
        if (inspectItem is null)
        {
            return new ContainerNetworkResource(listItem.Name, listItem.Driver, DateTimeOffset.MinValue, [], listItem.IsSystem);
        }

        return new ContainerNetworkResource(
            Name: string.IsNullOrEmpty(inspectItem.Name) ? listItem.Name : inspectItem.Name,
            Driver: string.IsNullOrEmpty(inspectItem.Driver) ? listItem.Driver : inspectItem.Driver,
            CreatedAt: CliDateTimeParsing.ParseDateTimeOffsetOrDefault(
                string.IsNullOrEmpty(inspectItem.Created) ? inspectItem.CreatedAt : inspectItem.Created),
            ConnectedContainerNames: [],
            IsSystem: listItem.IsSystem || inspectItem.IsSystem);
    }
}
