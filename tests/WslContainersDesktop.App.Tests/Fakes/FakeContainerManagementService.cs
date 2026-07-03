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
}
