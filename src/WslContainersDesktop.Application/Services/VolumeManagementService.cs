using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Services;

/// <summary>
/// <see cref="IVolumeManagementService"/> の実装。
/// </summary>
public sealed class VolumeManagementService : IVolumeManagementService
{
    private readonly IVolumeRuntimeClient _volumeClient;
    private readonly IContainerQueryClient _queryClient;

    public VolumeManagementService(IVolumeRuntimeClient volumeClient, IContainerQueryClient queryClient)
    {
        _volumeClient = volumeClient;
        _queryClient = queryClient;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContainerVolume>> GetVolumesAsync(CancellationToken cancellationToken = default)
    {
        var volumes = await _volumeClient.ListVolumesAsync(cancellationToken);
        if (volumes.Count == 0)
        {
            return volumes;
        }

        var referencesByVolume = await GetReferencesByVolumeAsync(volumes.Select(volume => volume.Name), cancellationToken);
        return volumes
            .Select(volume => volume with
            {
                ReferencingContainerNames = referencesByVolume.TryGetValue(volume.Name, out var references)
                    ? references
                    : [],
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ContainerVolume> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var trimmed = ValidateNotWhiteSpace(name, nameof(name));
        await _volumeClient.CreateVolumeAsync(trimmed, cancellationToken);

        var volumes = await GetVolumesAsync(cancellationToken);
        return volumes.FirstOrDefault(volume => volume.Name == trimmed)
            ?? new ContainerVolume(trimmed, string.Empty, DateTimeOffset.MinValue, []);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        var trimmed = ValidateNotWhiteSpace(name, nameof(name));
        var references = await GetReferencesForVolumeAsync(trimmed, cancellationToken);
        if (references.Count > 0)
        {
            throw new VolumeInUseException(trimmed, references);
        }

        await _volumeClient.DeleteVolumeAsync(trimmed, cancellationToken);
    }

    private async Task<IReadOnlyList<string>> GetReferencesForVolumeAsync(string volumeName, CancellationToken cancellationToken)
    {
        var referencesByVolume = await GetReferencesByVolumeAsync([volumeName], cancellationToken);
        return referencesByVolume.TryGetValue(volumeName, out var references) ? references : [];
    }

    private async Task<Dictionary<string, IReadOnlyList<string>>> GetReferencesByVolumeAsync(
        IEnumerable<string> volumeNames,
        CancellationToken cancellationToken)
    {
        var volumeNameList = volumeNames
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var referenceSets = CreateReferenceSets(volumeNameList);

        if (volumeNameList.Count == 0)
        {
            return ToReferenceMap(referenceSets);
        }

        var containers = await _queryClient.ListContainersAsync(cancellationToken);
        foreach (var container in containers)
        {
            ContainerDetail detail;
            try
            {
                detail = await _queryClient.GetContainerDetailAsync(container.Id, cancellationToken);
            }
            catch (ContainerManagementException)
            {
                continue;
            }

            foreach (var volumeName in volumeNameList)
            {
                if (detail.Mounts.Any(mount => SourceReferencesVolume(mount.Source, volumeName)))
                {
                    referenceSets[volumeName].Add(detail.Name);
                }
            }
        }

        return ToReferenceMap(referenceSets);
    }

    private static Dictionary<string, SortedSet<string>> CreateReferenceSets(IReadOnlyList<string> volumeNames)
    {
        return volumeNames.ToDictionary(
            volumeName => volumeName,
            _ => new SortedSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    private static Dictionary<string, IReadOnlyList<string>> ToReferenceMap(Dictionary<string, SortedSet<string>> referenceSets)
    {
        return referenceSets.ToDictionary(
            item => item.Key,
            item => (IReadOnlyList<string>)item.Value.ToList(),
            StringComparer.Ordinal);
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

    private static string ValidateNotWhiteSpace(string value, string parameterName)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("値を空白だけにすることはできません。", parameterName);
        }

        return trimmed;
    }
}
