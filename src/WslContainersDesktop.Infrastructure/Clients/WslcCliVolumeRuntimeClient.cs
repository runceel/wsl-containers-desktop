using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;
using WslContainersDesktop.Infrastructure.Cli;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc</c> CLIを利用してボリュームランタイムポートを実装する。
/// </summary>
public sealed class WslcCliVolumeRuntimeClient(IWslcCliRunner cliRunner) : IVolumeRuntimeClient
{
    /// ボリューム検査時に同時に実行する最大並列数。
    private const int InspectionConcurrencyLimit = 4;

    private readonly WslcCliCommandExecutor _executor = new(cliRunner);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ContainerVolume>> ListVolumesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _executor.RunAsync(["volume", "list", "--format", "json"], cancellationToken);

        var items = WslcCliCommandExecutor.DeserializeJsonList<VolumeListItemDto>(
            result,
            command: "volume list --format json",
            failureMessage: "コンテナーボリューム一覧の解析に失敗しました。");
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
        var item = items?.FirstOrDefault();
        if (item is null)
        {
            return new ContainerVolume(listItem.Name, listItem.Driver, DateTimeOffset.MinValue, []);
        }

        return new ContainerVolume(
            Name: string.IsNullOrEmpty(item.Name) ? listItem.Name : item.Name,
            Driver: string.IsNullOrEmpty(item.Driver) ? listItem.Driver : item.Driver,
            CreatedAt: CliDateTimeParsing.ParseDateTimeOffsetOrDefault(item.CreatedAt),
            ReferencingContainerNames: []);
    }
}
