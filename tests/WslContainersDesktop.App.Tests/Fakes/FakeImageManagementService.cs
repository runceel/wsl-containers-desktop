using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop_App_Tests.Fakes;

internal sealed class FakeImageManagementService : IImageManagementService
{
    private bool _usePullResultImages;
    private bool _useDeleteResultImages;

    public Queue<Func<Task<IReadOnlyList<ContainerImage>>>> GetImagesResults { get; } = new();

    public IReadOnlyList<ContainerImage> DefaultImages { get; set; } = [];

    public IReadOnlyList<ContainerImage> PullResultImages { get; set; } = [];

    public IReadOnlyList<ContainerImage> DeleteResultImages { get; set; } = [];

    public Exception? GetImagesException { get; set; }

    public Exception? PullException { get; set; }

    public Exception? DeleteException { get; set; }

    public TaskCompletionSource<bool>? PullGate { get; set; }

    public List<string> PullCalls { get; } = [];

    public List<string> DeleteCalls { get; } = [];

    public int GetImagesCallCount { get; private set; }

    public Task<IReadOnlyList<ContainerImage>> GetImagesAsync(CancellationToken cancellationToken = default)
    {
        GetImagesCallCount++;
        if (GetImagesResults.Count > 0)
        {
            return GetImagesResults.Dequeue()();
        }

        if (GetImagesException is not null)
        {
            throw GetImagesException;
        }

        if (_usePullResultImages)
        {
            _usePullResultImages = false;
            return Task.FromResult(PullResultImages);
        }

        if (_useDeleteResultImages)
        {
            _useDeleteResultImages = false;
            return Task.FromResult(DeleteResultImages);
        }

        return Task.FromResult(DefaultImages);
    }

    public async Task PullAsync(string reference, CancellationToken cancellationToken = default)
    {
        PullCalls.Add(reference);
        _usePullResultImages = true;
        _useDeleteResultImages = false;
        if (PullGate is not null)
        {
            await PullGate.Task;
        }

        if (PullException is not null)
        {
            throw PullException;
        }

        await Task.CompletedTask;
    }

    public Task DeleteAsync(string imageId, CancellationToken cancellationToken = default)
    {
        DeleteCalls.Add(imageId);
        _useDeleteResultImages = true;
        _usePullResultImages = false;
        if (DeleteException is not null)
        {
            throw DeleteException;
        }

        return Task.CompletedTask;
    }
}
