using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop_App_Tests.Fakes;

internal sealed class FakeVolumeManagementService : IVolumeManagementService
{
    public IReadOnlyList<ContainerVolume> DefaultVolumes { get; set; } = [];

    public ContainerVolume? CreateResult { get; set; }

    public Exception? GetVolumesException { get; set; }

    public Exception? CreateException { get; set; }

    public Exception? DeleteException { get; set; }

    public TaskCompletionSource<bool>? CreateGate { get; set; }

    public List<string> CreateCalls { get; } = [];

    public List<string> DeleteCalls { get; } = [];

    public Task<IReadOnlyList<ContainerVolume>> GetVolumesAsync(CancellationToken cancellationToken = default)
    {
        if (GetVolumesException is not null)
        {
            throw GetVolumesException;
        }

        return Task.FromResult(DefaultVolumes);
    }

    public async Task<ContainerVolume> CreateAsync(string name, CancellationToken cancellationToken = default)
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

        return CreateResult ?? new ContainerVolume(name, "guest", DateTimeOffset.UtcNow, []);
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
