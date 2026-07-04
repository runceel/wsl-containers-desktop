using System.Runtime.CompilerServices;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Tests.Fakes;

/// <summary>
/// <see cref="IContainerRuntimeClient"/> のテスト用フェイク実装。
/// 呼び出された引数を記録し、任意の例外をスローできる。
/// </summary>
internal sealed class FakeContainerRuntimeClient : IContainerRuntimeClient
{
    public IReadOnlyList<Container> Containers { get; set; } = [];

    public IReadOnlyList<ContainerImage> Images { get; set; } = [];

    public IReadOnlyList<ContainerVolume> Volumes { get; set; } = [];

    public IReadOnlyList<ContainerNetworkResource> Networks { get; set; } = [];

    public IReadOnlyDictionary<string, ContainerDetail> ContainerDetailsById { get; set; } = new Dictionary<string, ContainerDetail>();

    public Exception? ExceptionToThrow { get; set; }

    public Exception? PullException { get; set; }

    public Exception? DeleteImageException { get; set; }

    public IReadOnlyList<string> ContainerLogs { get; set; } = [];

    public ContainerDetail? ContainerDetail { get; set; }

    public IContainerExecSession? ExecSession { get; set; }

    public Exception? GetContainerLogsException { get; set; }

    public Exception? FollowContainerLogsException { get; set; }

    public Exception? GetContainerDetailException { get; set; }

    public Exception? DeleteVolumeException { get; set; }

    public Exception? CreateNetworkException { get; set; }

    public Exception? DeleteNetworkException { get; set; }

    public Func<string, CancellationToken, Task<IReadOnlyList<string>>>? GetContainerLogsAsyncFunc { get; set; }

    public Func<string, CancellationToken, IAsyncEnumerable<string>>? FollowContainerLogsAsyncFunc { get; set; }

    public List<string> StartCalls { get; } = [];

    public List<string> StopCalls { get; } = [];

    public List<string> DeleteCalls { get; } = [];

    public List<string> PullCalls { get; } = [];

    public List<string> DeleteImageCalls { get; } = [];

    public List<string> GetContainerLogsCalls { get; } = [];

    public List<string> GetContainerDetailCalls { get; } = [];

    public List<string> OpenExecSessionCalls { get; } = [];

    public List<string> FollowContainerLogsCalls { get; } = [];

    public List<string> CreateVolumeCalls { get; } = [];

    public List<string> DeleteVolumeCalls { get; } = [];

    public List<string> CreateNetworkCalls { get; } = [];

    public List<string> DeleteNetworkCalls { get; } = [];

    public Task<IReadOnlyList<Container>> ListContainersAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Containers);
    }

    public Task<IReadOnlyList<ContainerImage>> ListImagesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Images);
    }

    public Task PullImageAsync(string reference, CancellationToken cancellationToken = default)
    {
        PullCalls.Add(reference);
        if (PullException is not null)
        {
            throw PullException;
        }

        return Task.CompletedTask;
    }

    public Task DeleteImageAsync(string imageId, CancellationToken cancellationToken = default)
    {
        DeleteImageCalls.Add(imageId);
        if (DeleteImageException is not null)
        {
            throw DeleteImageException;
        }

        return Task.CompletedTask;
    }

    public Task StartAsync(string containerId, CancellationToken cancellationToken = default)
    {
        StartCalls.Add(containerId);
        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(string containerId, CancellationToken cancellationToken = default)
    {
        StopCalls.Add(containerId);
        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string containerId, CancellationToken cancellationToken = default)
    {
        DeleteCalls.Add(containerId);
        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetContainerLogsAsync(string containerId, CancellationToken cancellationToken = default)
    {
        GetContainerLogsCalls.Add(containerId);
        if (GetContainerLogsException is not null)
        {
            throw GetContainerLogsException;
        }

        if (GetContainerLogsAsyncFunc is not null)
        {
            return GetContainerLogsAsyncFunc(containerId, cancellationToken);
        }

        return Task.FromResult(ContainerLogs);
    }

    public Task<ContainerDetail> GetContainerDetailAsync(string containerId, CancellationToken cancellationToken = default)
    {
        GetContainerDetailCalls.Add(containerId);
        if (GetContainerDetailException is not null)
        {
            throw GetContainerDetailException;
        }

        if (ContainerDetailsById.TryGetValue(containerId, out var detail))
        {
            return Task.FromResult(detail);
        }

        return Task.FromResult(ContainerDetail!);
    }

    public Task<IContainerExecSession> OpenExecSessionAsync(string containerId, CancellationToken cancellationToken = default)
    {
        OpenExecSessionCalls.Add(containerId);
        return Task.FromResult(ExecSession!);
    }

    public async IAsyncEnumerable<string> FollowContainerLogsAsync(string containerId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        FollowContainerLogsCalls.Add(containerId);
        if (FollowContainerLogsException is not null)
        {
            throw FollowContainerLogsException;
        }

        if (FollowContainerLogsAsyncFunc is not null)
        {
            await foreach (var line in FollowContainerLogsAsyncFunc(containerId, cancellationToken))
            {
                yield return line;
            }

            yield break;
        }

        foreach (var line in ContainerLogs)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            yield return line;
        }
    }

    public Task<IReadOnlyList<ContainerVolume>> ListVolumesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Volumes);
    }

    public Task CreateVolumeAsync(string name, CancellationToken cancellationToken = default)
    {
        CreateVolumeCalls.Add(name);
        return Task.CompletedTask;
    }

    public Task DeleteVolumeAsync(string name, CancellationToken cancellationToken = default)
    {
        DeleteVolumeCalls.Add(name);
        if (DeleteVolumeException is not null)
        {
            throw DeleteVolumeException;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ContainerNetworkResource>> ListNetworksAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Networks);
    }

    public Task CreateNetworkAsync(string name, CancellationToken cancellationToken = default)
    {
        CreateNetworkCalls.Add(name);
        if (CreateNetworkException is not null)
        {
            throw CreateNetworkException;
        }

        return Task.CompletedTask;
    }

    public Task DeleteNetworkAsync(string name, CancellationToken cancellationToken = default)
    {
        DeleteNetworkCalls.Add(name);
        if (DeleteNetworkException is not null)
        {
            throw DeleteNetworkException;
        }

        return Task.CompletedTask;
    }
}
