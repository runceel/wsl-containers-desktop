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

    public Exception? ExceptionToThrow { get; set; }

    public IReadOnlyList<string> ContainerLogs { get; set; } = [];

    public Exception? GetContainerLogsException { get; set; }

    public Exception? FollowContainerLogsException { get; set; }

    public Func<string, CancellationToken, Task<IReadOnlyList<string>>>? GetContainerLogsAsyncFunc { get; set; }

    public Func<string, CancellationToken, IAsyncEnumerable<string>>? FollowContainerLogsAsyncFunc { get; set; }

    public List<string> StartCalls { get; } = [];

    public List<string> StopCalls { get; } = [];

    public List<string> DeleteCalls { get; } = [];

    public List<string> GetContainerLogsCalls { get; } = [];

    public List<string> FollowContainerLogsCalls { get; } = [];

    public Task<IReadOnlyList<Container>> ListContainersAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Containers);
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
}
