using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Services;

/// <summary>
/// <see cref="INetworkManagementService"/> の実装。
/// </summary>
public sealed class NetworkManagementService : INetworkManagementService
{
    private static readonly HashSet<string> ReservedSystemNetworkNames = new(StringComparer.Ordinal)
    {
        "bridge",
        "host",
        "none",
    };

    private readonly INetworkRuntimeClient _networkClient;
    private readonly IContainerQueryClient _queryClient;

    public NetworkManagementService(INetworkRuntimeClient networkClient, IContainerQueryClient queryClient)
    {
        _networkClient = networkClient;
        _queryClient = queryClient;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContainerNetworkResource>> GetNetworksAsync(CancellationToken cancellationToken = default)
    {
        var networks = await _networkClient.ListNetworksAsync(cancellationToken);
        if (networks.Count == 0)
        {
            return networks;
        }

        var connectedContainersByNetwork = await GetConnectedContainersByNetworkAsync(
            networks.Select(network => network.Name),
            cancellationToken);

        return networks
            .Select(network => network with
            {
                ConnectedContainerNames = connectedContainersByNetwork.TryGetValue(network.Name, out var connections)
                    ? connections
                    : [],
                IsSystem = network.IsSystem || IsReservedSystemNetworkName(network.Name),
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ContainerNetworkResource> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var trimmed = ValidateNotWhiteSpace(name, nameof(name));
        await _networkClient.CreateNetworkAsync(trimmed, cancellationToken);

        var networks = await GetNetworksAsync(cancellationToken);
        return networks.FirstOrDefault(network => network.Name == trimmed)
            ?? new ContainerNetworkResource(trimmed, string.Empty, DateTimeOffset.MinValue, [], false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        var trimmed = ValidateNotWhiteSpace(name, nameof(name));
        var networks = await GetNetworksAsync(cancellationToken);
        var target = networks.FirstOrDefault(network => network.Name == trimmed);
        if (target is not null)
        {
            if (target.IsSystem)
            {
                throw new SystemNetworkDeletionException(trimmed);
            }

            if (target.IsInUse)
            {
                throw new NetworkInUseException(trimmed, target.ConnectedContainerNames);
            }
        }

        await _networkClient.DeleteNetworkAsync(trimmed, cancellationToken);
    }

    private async Task<Dictionary<string, IReadOnlyList<string>>> GetConnectedContainersByNetworkAsync(
        IEnumerable<string> networkNames,
        CancellationToken cancellationToken)
    {
        var networkNameList = networkNames
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var connectionSets = networkNameList.ToDictionary(
            networkName => networkName,
            _ => new SortedSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        if (networkNameList.Count == 0)
        {
            return ToConnectionMap(connectionSets);
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

            foreach (var containerNetwork in detail.Networks)
            {
                if (connectionSets.TryGetValue(containerNetwork.Name, out var connectedContainerNames))
                {
                    connectedContainerNames.Add(detail.Name);
                }
            }
        }

        return ToConnectionMap(connectionSets);
    }

    private static Dictionary<string, IReadOnlyList<string>> ToConnectionMap(Dictionary<string, SortedSet<string>> connectionSets)
    {
        return connectionSets.ToDictionary(
            item => item.Key,
            item => (IReadOnlyList<string>)item.Value.ToList(),
            StringComparer.Ordinal);
    }

    private static bool IsReservedSystemNetworkName(string name)
    {
        return ReservedSystemNetworkNames.Contains(name);
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
