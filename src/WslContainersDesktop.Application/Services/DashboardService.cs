using System.Collections.Concurrent;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Services;

/// <summary>
/// ダッシュボード表示用の情報をまとめて取得するユースケース。
/// </summary>
public sealed class DashboardService(IContainerRuntimeClient runtimeClient) : IDashboardService
{
    private const int ContainerDetailInspectionConcurrencyLimit = 4;

    /// <inheritdoc />
    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var containersTask = RunRootOperationAsync(runtimeClient.ListContainersAsync, cancellationToken);
        var imagesTask = RunRootOperationAsync(runtimeClient.ListImagesAsync, cancellationToken);
        var volumesTask = RunRootOperationAsync(runtimeClient.ListVolumesAsync, cancellationToken);
        var networksTask = RunRootOperationAsync(runtimeClient.ListNetworksAsync, cancellationToken);
        var statsTask = RunRootOperationAsync(runtimeClient.GetContainerStatsAsync, cancellationToken);

        var containersSection = await containersTask;
        var imagesSection = await imagesTask;
        var volumesSection = await volumesTask;
        var networksSection = await networksTask;
        var statsSection = await statsTask;

        var snapshot = new DashboardSnapshot
        {
            Containers = containersSection,
            Images = imagesSection,
            Volumes = volumesSection,
            Networks = networksSection,
            Stats = statsSection,
        };

        if (containersSection.Exception is null)
        {
            var containerDetails = await GetContainerDetailsAsync(containersSection.Value, cancellationToken);
            if (containerDetails.Count > 0)
            {
                if (volumesSection.Exception is null)
                {
                    snapshot.Volumes = new DashboardSection<IReadOnlyList<ContainerVolume>>
                    {
                        Value = EnrichVolumes(volumesSection.Value, containerDetails),
                    };
                }

                if (networksSection.Exception is null)
                {
                    snapshot.Networks = new DashboardSection<IReadOnlyList<ContainerNetworkResource>>
                    {
                        Value = EnrichNetworks(networksSection.Value, containerDetails),
                    };
                }
            }
        }

        return snapshot;
    }

    private async Task<DashboardSection<IReadOnlyList<TItem>>> RunRootOperationAsync<TItem>(
        Func<CancellationToken, Task<IReadOnlyList<TItem>>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            var value = await operation(cancellationToken);
            return new DashboardSection<IReadOnlyList<TItem>>
            {
                Value = value ?? [],
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new DashboardSection<IReadOnlyList<TItem>>
            {
                Value = [],
                Exception = ex,
            };
        }
    }

    private async Task<IReadOnlyDictionary<string, ContainerDetail>> GetContainerDetailsAsync(
        IReadOnlyList<Container> containers,
        CancellationToken cancellationToken)
    {
        if (containers.Count == 0)
        {
            return new Dictionary<string, ContainerDetail>(StringComparer.Ordinal);
        }

        var detailsByContainerId = new ConcurrentDictionary<string, ContainerDetail>(StringComparer.Ordinal);
        var semaphore = new SemaphoreSlim(ContainerDetailInspectionConcurrencyLimit);
        var tasks = containers.Select(container => InspectDetailAsync(container, semaphore, detailsByContainerId, cancellationToken));
        await Task.WhenAll(tasks);
        return detailsByContainerId.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
    }

    private async Task InspectDetailAsync(
        Container container,
        SemaphoreSlim semaphore,
        ConcurrentDictionary<string, ContainerDetail> detailsByContainerId,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            ContainerDetail detail;
            try
            {
                detail = await runtimeClient.GetContainerDetailAsync(container.Id, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return;
            }

            detailsByContainerId[container.Id] = detail;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static IReadOnlyList<ContainerVolume> EnrichVolumes(
        IReadOnlyList<ContainerVolume> volumes,
        IReadOnlyDictionary<string, ContainerDetail> detailsByContainerId)
    {
        if (volumes.Count == 0)
        {
            return volumes;
        }

        var referenceSets = volumes
            .Select(volume => volume.Name)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(
                volumeName => volumeName,
                _ => new SortedSet<string>(StringComparer.Ordinal),
                StringComparer.Ordinal);

        foreach (var detail in detailsByContainerId.Values)
        {
            foreach (var volumeName in referenceSets.Keys.ToList())
            {
                if (detail.Mounts.Any(mount => SourceReferencesVolume(mount.Source, volumeName)))
                {
                    referenceSets[volumeName].Add(detail.Name);
                }
            }
        }

        return volumes
            .Select(volume => volume with
            {
                ReferencingContainerNames = referenceSets.TryGetValue(volume.Name, out var references)
                    ? references.ToList()
                    : []
            })
            .ToList();
    }

    private static IReadOnlyList<ContainerNetworkResource> EnrichNetworks(
        IReadOnlyList<ContainerNetworkResource> networks,
        IReadOnlyDictionary<string, ContainerDetail> detailsByContainerId)
    {
        if (networks.Count == 0)
        {
            return networks;
        }

        var connectionSets = networks
            .Select(network => network.Name)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(
                networkName => networkName,
                _ => new SortedSet<string>(StringComparer.Ordinal),
                StringComparer.Ordinal);

        foreach (var detail in detailsByContainerId.Values)
        {
            foreach (var containerNetwork in detail.Networks)
            {
                if (connectionSets.TryGetValue(containerNetwork.Name, out var connectedContainerNames))
                {
                    connectedContainerNames.Add(detail.Name);
                }
            }
        }

        return networks
            .Select(network => network with
            {
                ConnectedContainerNames = connectionSets.TryGetValue(network.Name, out var connections)
                    ? connections.ToList()
                    : []
            })
            .ToList();
    }

    private static bool SourceReferencesVolume(string source, string volumeName)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(volumeName))
        {
            return false;
        }

        var normalized = source.Replace('\\', '/');
        if (normalized.EndsWith($"/volumes/{volumeName}/_data", StringComparison.Ordinal))
        {
            return true;
        }

        return normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment == volumeName);
    }
}
