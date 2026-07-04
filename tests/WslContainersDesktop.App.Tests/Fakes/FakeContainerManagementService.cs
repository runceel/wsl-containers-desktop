using System.Runtime.CompilerServices;
using System.Threading.Channels;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop_App_Tests.Fakes;

/// <summary>
/// <see cref="IContainerManagementService"/> のテスト用フェイク実装。
/// 呼び出し回数に応じて異なる結果を返せるよう <see cref="GetContainersResults"/> をキューとして保持する。
/// </summary>
internal sealed class FakeContainerManagementService : IContainerManagementService
{
    /// <summary>
    /// <see cref="GetContainersAsync"/> の呼び出しごとに順番に消費される結果。
    /// 空になった後は <see cref="DefaultContainers"/> を返す。
    /// </summary>
    public Queue<Func<Task<IReadOnlyList<Container>>>> GetContainersResults { get; } = new();

    public IReadOnlyList<Container> DefaultContainers { get; set; } = [];

    public Exception? StartException { get; set; }

    public Exception? StopException { get; set; }

    public Exception? RestartException { get; set; }

    public Exception? DeleteException { get; set; }

    public TaskCompletionSource<Container>? StartAsyncGate { get; set; }

    public TaskCompletionSource<Container>? StopAsyncGate { get; set; }

    public TaskCompletionSource<Container>? RestartAsyncGate { get; set; }

    public TaskCompletionSource<bool>? DeleteAsyncGate { get; set; }

    public Container? StartResult { get; set; }

    public Container? StopResult { get; set; }

    public Container? RestartResult { get; set; }

    public List<string> DeleteCalls { get; } = [];

    public IReadOnlyList<string> DefaultLogs { get; set; } = [];

    public ContainerDetail? ContainerDetail { get; set; }

    public IContainerExecSession? ExecSession { get; set; }

    public Queue<IContainerExecSession> OpenExecSessionResults { get; } = new();

    public Exception? GetContainerDetailException { get; set; }

    public Exception? OpenExecSessionException { get; set; }

    public bool RecordFailedOpenExecSessionCalls { get; set; }

    public Exception? GetContainerLogsException { get; set; }

    public Exception? FollowContainerLogsException { get; set; }

    public Channel<string>? FollowLogsChannel { get; set; }

    public TaskCompletionSource<bool>? FollowContainerLogsStarted { get; set; }

    public int FollowCancellationCount { get; private set; }

    public List<string> GetContainerLogsCalls { get; } = [];

    public List<string> GetContainerDetailCalls { get; } = [];

    public List<string> OpenExecSessionCalls { get; } = [];

    public List<string> FollowContainerLogsCalls { get; } = [];

    public Task<IReadOnlyList<Container>> GetContainersAsync(CancellationToken cancellationToken = default)
    {
        if (GetContainersResults.Count > 0)
        {
            return GetContainersResults.Dequeue()();
        }

        return Task.FromResult(DefaultContainers);
    }

    public async Task<Container> StartAsync(string containerId, CancellationToken cancellationToken = default)
    {
        if (StartAsyncGate is not null)
        {
            await StartAsyncGate.Task;
        }

        if (StartException is not null)
        {
            throw StartException;
        }

        return StartResult!;
    }

    public async Task<Container> StopAsync(string containerId, CancellationToken cancellationToken = default)
    {
        if (StopAsyncGate is not null)
        {
            await StopAsyncGate.Task;
        }

        if (StopException is not null)
        {
            throw StopException;
        }

        return StopResult!;
    }

    public async Task<Container> RestartAsync(string containerId, CancellationToken cancellationToken = default)
    {
        if (RestartAsyncGate is not null)
        {
            await RestartAsyncGate.Task;
        }

        if (RestartException is not null)
        {
            throw RestartException;
        }

        return RestartResult!;
    }

    public async Task DeleteAsync(string containerId, CancellationToken cancellationToken = default)
    {
        DeleteCalls.Add(containerId);

        if (DeleteAsyncGate is not null)
        {
            await DeleteAsyncGate.Task;
        }

        if (DeleteException is not null)
        {
            throw DeleteException;
        }
    }

    public Task<IReadOnlyList<string>> GetContainerLogsAsync(string containerId, CancellationToken cancellationToken = default)
    {
        GetContainerLogsCalls.Add(containerId);
        if (GetContainerLogsException is not null)
        {
            throw GetContainerLogsException;
        }

        return Task.FromResult(DefaultLogs);
    }

    public Task<ContainerDetail> GetContainerDetailAsync(string containerId, CancellationToken cancellationToken = default)
    {
        GetContainerDetailCalls.Add(containerId);
        if (GetContainerDetailException is not null)
        {
            throw GetContainerDetailException;
        }

        return Task.FromResult(ContainerDetail!);
    }

    public Task<IContainerExecSession> OpenExecSessionAsync(string containerId, CancellationToken cancellationToken = default)
    {
        if (OpenExecSessionException is not null)
        {
            if (RecordFailedOpenExecSessionCalls)
            {
                OpenExecSessionCalls.Add(containerId);
            }

            throw OpenExecSessionException;
        }

        OpenExecSessionCalls.Add(containerId);
        if (OpenExecSessionResults.Count > 0)
        {
            return Task.FromResult(OpenExecSessionResults.Dequeue());
        }

        return Task.FromResult(ExecSession!);
    }

    public async IAsyncEnumerable<string> FollowContainerLogsAsync(string containerId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        FollowContainerLogsCalls.Add(containerId);
        FollowContainerLogsStarted?.TrySetResult(true);

        if (FollowContainerLogsException is not null)
        {
            throw FollowContainerLogsException;
        }

        if (FollowLogsChannel is null)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                FollowCancellationCount++;
                throw;
            }

            yield break;
        }

        while (true)
        {
            try
            {
                if (!await FollowLogsChannel.Reader.WaitToReadAsync(cancellationToken))
                {
                    yield break;
                }
            }
            catch (OperationCanceledException)
            {
                FollowCancellationCount++;
                throw;
            }

            while (FollowLogsChannel.Reader.TryRead(out var line))
            {
                yield return line;
            }
        }
    }
}
