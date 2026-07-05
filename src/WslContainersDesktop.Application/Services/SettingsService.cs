using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Services;

/// <summary>
/// <see cref="ISettingsService"/> の実装。要件充足の判定ポリシーを所有し、
/// 環境情報の観測（<see cref="IWslEnvironmentProbe"/>）とリソース制限の永続化
/// （<see cref="IWslResourceLimitsStore"/>）を協調させる。
/// </summary>
public sealed class SettingsService(IWslEnvironmentProbe environmentProbe, IWslResourceLimitsStore resourceLimitsStore)
    : ISettingsService
{
    /// <summary>
    /// WSL Containersを利用するために必要なWSLの最小バージョン。
    /// </summary>
    private static readonly Version MinimumWslContainersVersion = new(2, 9, 3);

    /// <inheritdoc />
    public async Task<WslIntegrationStatus> GetIntegrationStatusAsync(CancellationToken cancellationToken = default)
    {
        var info = await environmentProbe.GetEnvironmentInfoAsync(cancellationToken);
        var meetsRequirements = info.IsWslContainersAvailable
            && Version.TryParse(info.WslVersion, out var version)
            && version >= MinimumWslContainersVersion;

        return new WslIntegrationStatus(info.WslVersion, info.IsWslContainersAvailable, meetsRequirements);
    }

    /// <inheritdoc />
    public Task<WslResourceLimits> GetResourceLimitsAsync(CancellationToken cancellationToken = default)
    {
        return resourceLimitsStore.GetAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveResourceLimitsAsync(WslResourceLimits limits, CancellationToken cancellationToken = default)
    {
        await EnsureRequirementsMetAsync(cancellationToken);

        if (!limits.IsValid)
        {
            throw new InvalidResourceLimitsException();
        }

        await resourceLimitsStore.SaveAsync(limits, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ResetResourceLimitsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureRequirementsMetAsync(cancellationToken);
        await resourceLimitsStore.SaveAsync(WslResourceLimits.Defaults, cancellationToken);
    }

    private async Task EnsureRequirementsMetAsync(CancellationToken cancellationToken)
    {
        var status = await GetIntegrationStatusAsync(cancellationToken);
        if (!status.CanConfigureResources)
        {
            throw new WslRequirementsNotMetException();
        }
    }
}
