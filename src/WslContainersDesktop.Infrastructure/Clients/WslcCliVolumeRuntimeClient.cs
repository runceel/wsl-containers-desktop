using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;
using WslContainersDesktop.Infrastructure.Cli;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc</c> CLIを利用してボリュームランタイムポートを実装する。
/// </summary>
public sealed class WslcCliVolumeRuntimeClient(IWslcCliRunner cliRunner) : IVolumeRuntimeClient
{
    private static readonly string[] ListVolumesCommand = ["volume", "list", "--format", "json"];

    private const string ListVolumesCommandText = "volume list --format json";

    /// ボリューム検査時に同時に実行する最大並列数。
    private const int InspectionConcurrencyLimit = 4;

    private readonly WslcCliCommandExecutor _executor = new(cliRunner);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ContainerVolume>> ListVolumesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _executor.RunAsync(ListVolumesCommand, cancellationToken);

        var items = WslcCliCommandExecutor.DeserializeJsonList<VolumeListItemDto>(
            result,
            command: ListVolumesCommandText,
            failureMessage: "コンテナーボリューム一覧の解析に失敗しました。",
            allowEmptyOutput: true,
            allowJsonLines: true);
        if (items is null)
        {
            return [];
        }

        return await BoundedConcurrencyInspector.InspectAsync(
            items,
            InspectVolumeAsync,
            InspectionConcurrencyLimit,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task CreateVolumeAsync(string name, CancellationToken cancellationToken = default)
    {
        return _executor.RunAsync(["volume", "create", name], cancellationToken);
    }

    /// <inheritdoc/>
    public Task DeleteVolumeAsync(string name, CancellationToken cancellationToken = default)
    {
        return _executor.RunAsync(["volume", "remove", name], cancellationToken);
    }

    private async Task<ContainerVolume> InspectVolumeAsync(VolumeListItemDto listItem, CancellationToken cancellationToken)
    {
        var result = await _executor.RunAsync(["volume", "inspect", listItem.Name], cancellationToken);

        var items = WslcCliCommandExecutor.DeserializeJsonList<VolumeInspectDto>(
            result,
            command: $"volume inspect {listItem.Name}",
            failureMessage: "コンテナーボリューム詳細情報の解析に失敗しました。");
        return MapVolume(listItem, items?.FirstOrDefault());
    }

    private static ContainerVolume MapVolume(VolumeListItemDto listItem, VolumeInspectDto? inspectItem)
    {
        if (inspectItem is null)
        {
            return new ContainerVolume(listItem.Name, listItem.Driver, DateTimeOffset.MinValue, []);
        }

        return new ContainerVolume(
            Name: string.IsNullOrEmpty(inspectItem.Name) ? listItem.Name : inspectItem.Name,
            Driver: string.IsNullOrEmpty(inspectItem.Driver) ? listItem.Driver : inspectItem.Driver,
            CreatedAt: CliDateTimeParsing.ParseDateTimeOffsetOrDefault(inspectItem.CreatedAt),
            ReferencingContainerNames: []);
    }
}
