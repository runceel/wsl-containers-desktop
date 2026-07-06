using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;
using System.Runtime.CompilerServices;

namespace WslContainersDesktop.Application.Services;

/// <summary>
/// <see cref="IContainerManagementService"/> の実装。
/// 各操作の前に最新の一覧を取得してコンテナの現在状態を検証してから
/// <see cref="IContainerRuntimeClient"/> を呼び出す。
/// </summary>
public sealed class ContainerManagementService(IContainerRuntimeClient runtimeClient) : IContainerManagementService
{
    public Task<IReadOnlyList<ContainerResourceUsage>> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        return runtimeClient.GetContainerStatsAsync(cancellationToken);
    }

    public Task<IReadOnlyList<Container>> GetContainersAsync(CancellationToken cancellationToken = default)
    {
        return runtimeClient.ListContainersAsync(cancellationToken);
    }

    public async Task<Container> StartAsync(string containerId, CancellationToken cancellationToken = default)
    {
        var container = await FindContainerAsync(containerId, cancellationToken);
        if (!container.CanStart)
        {
            throw new InvalidContainerOperationException(containerId, nameof(StartAsync));
        }

        await runtimeClient.StartAsync(containerId, cancellationToken);
        return container with { State = ContainerState.Running };
    }

    public async Task<Container> StopAsync(string containerId, CancellationToken cancellationToken = default)
    {
        var container = await FindContainerAsync(containerId, cancellationToken);
        if (!container.CanStop)
        {
            throw new InvalidContainerOperationException(containerId, nameof(StopAsync));
        }

        await runtimeClient.StopAsync(containerId, cancellationToken);
        return container with { State = ContainerState.Stopped };
    }

    public async Task<Container> RestartAsync(string containerId, CancellationToken cancellationToken = default)
    {
        var container = await FindContainerAsync(containerId, cancellationToken);
        if (!container.CanRestart)
        {
            // wslcにはrestartサブコマンドが存在しないため stop→start で代替するが、
            // 事前に実行中であることを検証しないと、既に停止済みのコンテナに対しては
            // 「起動」にすり替わって成功してしまう（ADR-0009参照）。
            throw new InvalidContainerOperationException(containerId, nameof(RestartAsync));
        }

        await runtimeClient.StopAsync(containerId, cancellationToken);
        await runtimeClient.StartAsync(containerId, cancellationToken);
        return container with { State = ContainerState.Running };
    }

    public async Task DeleteAsync(string containerId, CancellationToken cancellationToken = default)
    {
        var container = await FindContainerAsync(containerId, cancellationToken);
        if (!container.CanDelete)
        {
            throw new InvalidContainerOperationException(containerId, nameof(DeleteAsync));
        }

        await runtimeClient.DeleteAsync(containerId, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetContainerLogsAsync(string containerId, CancellationToken cancellationToken = default)
    {
        await EnsureContainerExistsAsync(containerId, cancellationToken);
        return await runtimeClient.GetContainerLogsAsync(containerId, cancellationToken);
    }

    public async Task<ContainerDetail> GetContainerDetailAsync(string containerId, CancellationToken cancellationToken = default)
    {
        await EnsureContainerExistsAsync(containerId, cancellationToken);
        return await runtimeClient.GetContainerDetailAsync(containerId, cancellationToken);
    }

    public async Task<IContainerExecSession> OpenExecSessionAsync(string containerId, CancellationToken cancellationToken = default)
    {
        var container = await FindContainerAsync(containerId, cancellationToken);
        if (container.State != ContainerState.Running)
        {
            throw new InvalidContainerOperationException(containerId, nameof(OpenExecSessionAsync));
        }

        return await runtimeClient.OpenExecSessionAsync(containerId, cancellationToken);
    }

    public async IAsyncEnumerable<string> FollowContainerLogsAsync(
        string containerId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureContainerExistsAsync(containerId, cancellationToken);
        await foreach (var line in runtimeClient.FollowContainerLogsAsync(containerId, cancellationToken))
        {
            yield return line;
        }
    }

    private async Task EnsureContainerExistsAsync(string containerId, CancellationToken cancellationToken)
    {
        _ = await FindContainerAsync(containerId, cancellationToken);
    }

    private async Task<Container> FindContainerAsync(string containerId, CancellationToken cancellationToken)
    {
        var containers = await runtimeClient.ListContainersAsync(cancellationToken);
        foreach (var container in containers)
        {
            if (container.Id == containerId)
            {
                return container;
            }
        }

        throw new ContainerNotFoundException(containerId);
    }
}
