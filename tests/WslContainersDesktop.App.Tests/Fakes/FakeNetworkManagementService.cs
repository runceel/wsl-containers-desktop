using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop_App_Tests.Fakes;

internal sealed class FakeNetworkManagementService : INetworkManagementService
{
    public IReadOnlyList<ContainerNetworkResource> DefaultNetworks { get; set; } = [];

    public ContainerNetworkResource? CreateResult { get; set; }

    public Exception? GetNetworksException { get; set; }

    public Exception? CreateException { get; set; }

    public Exception? DeleteException { get; set; }

    public TaskCompletionSource<bool>? CreateGate { get; set; }

    public List<string> CreateCalls { get; } = [];

    public List<string> DeleteCalls { get; } = [];

    public int GetNetworksCallCount { get; private set; }

    public Task<IReadOnlyList<ContainerNetworkResource>> GetNetworksAsync(CancellationToken cancellationToken = default)
    {
        GetNetworksCallCount++;
        if (GetNetworksException is not null)
        {
            throw GetNetworksException;
        }

        return Task.FromResult(DefaultNetworks);
    }

    public async Task<ContainerNetworkResource> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        CreateCalls.Add(name);
        if (CreateGate is not null)
        {
            await CreateGate.Task;
        }

        if (CreateException is not null)
        {
            throw CreateException;
        }

        return CreateResult ?? new ContainerNetworkResource(name, "bridge", DateTimeOffset.UtcNow, [], false);
    }

    public Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        DeleteCalls.Add(name);
        if (DeleteException is not null)
        {
            throw DeleteException;
        }

        return Task.CompletedTask;
    }
}
