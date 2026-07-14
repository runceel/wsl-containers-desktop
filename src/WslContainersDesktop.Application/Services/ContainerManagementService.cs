using System.Runtime.CompilerServices;
using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Services;

/// <summary>
/// <see cref="IContainerManagementService"/> の実装。
/// 各操作の前に最新の一覧を取得してコンテナの現在状態を検証してから
/// フォーカスドなランタイムポートを呼び出す。
/// </summary>
public sealed class ContainerManagementService : IContainerManagementService
{
    private readonly IContainerQueryClient _queryClient;
    private readonly IContainerLifecycleClient _lifecycleClient;
    private readonly IContainerLogClient _logClient;
    private readonly IContainerExecClient _execClient;
    private readonly IContainerStatsClient _statsClient;

    public ContainerManagementService(
        IContainerQueryClient queryClient,
        IContainerLifecycleClient lifecycleClient,
        IContainerLogClient logClient,
        IContainerExecClient execClient,
        IContainerStatsClient statsClient)
    {
        _queryClient = queryClient;
        _lifecycleClient = lifecycleClient;
        _logClient = logClient;
        _execClient = execClient;
        _statsClient = statsClient;
    }

    public Task<IReadOnlyList<ContainerResourceUsage>> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        return _statsClient.GetContainerStatsAsync(cancellationToken);
    }

    public Task<IReadOnlyList<Container>> GetContainersAsync(CancellationToken cancellationToken = default)
    {
        return _queryClient.ListContainersAsync(cancellationToken);
    }

    public Task RunAsync(ContainerRunRequest request, CancellationToken cancellationToken = default)
    {
        var imageReference = request.ImageReference.Trim();
        if (imageReference.Length == 0)
        {
            throw new ArgumentException("Image reference is required.", nameof(request));
        }

        var normalizedRequest = new ContainerRunRequest
        {
            ImageReference = imageReference,
            ContainerName = request.ContainerName.Trim(),
            RemoveWhenStopped = request.RemoveWhenStopped,
            PortMappings = TrimAndRemoveEmpty(request.PortMappings),
            EnvironmentVariables = TrimAndRemoveEmpty(request.EnvironmentVariables),
            Command = request.Command.Trim(),
        };

        return _lifecycleClient.RunContainerAsync(normalizedRequest, cancellationToken);
    }

    public async Task<Container> StartAsync(string containerId, CancellationToken cancellationToken = default)
    {
        var container = await FindContainerAsync(containerId, cancellationToken);
        if (!container.CanStart)
        {
            throw new InvalidContainerOperationException(containerId, nameof(StartAsync));
        }

        await _lifecycleClient.StartAsync(containerId, cancellationToken);
        return container with { State = ContainerState.Running };
    }

    public async Task<Container> StopAsync(string containerId, CancellationToken cancellationToken = default)
    {
        var container = await FindContainerAsync(containerId, cancellationToken);
        if (!container.CanStop)
        {
            throw new InvalidContainerOperationException(containerId, nameof(StopAsync));
        }

        await _lifecycleClient.StopAsync(containerId, cancellationToken);
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

        await _lifecycleClient.StopAsync(containerId, cancellationToken);
        await _lifecycleClient.StartAsync(containerId, cancellationToken);
        return container with { State = ContainerState.Running };
    }

    public async Task DeleteAsync(string containerId, CancellationToken cancellationToken = default)
    {
        var container = await FindContainerAsync(containerId, cancellationToken);
        if (!container.CanDelete)
        {
            throw new InvalidContainerOperationException(containerId, nameof(DeleteAsync));
        }

        await _lifecycleClient.DeleteAsync(containerId, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetContainerLogsAsync(string containerId, CancellationToken cancellationToken = default)
    {
        await EnsureContainerExistsAsync(containerId, cancellationToken);
        return await _logClient.GetContainerLogsAsync(containerId, cancellationToken);
    }

    public async Task<ContainerDetail> GetContainerDetailAsync(string containerId, CancellationToken cancellationToken = default)
    {
        await EnsureContainerExistsAsync(containerId, cancellationToken);
        return await _queryClient.GetContainerDetailAsync(containerId, cancellationToken);
    }

    public async Task<IContainerExecSession> OpenExecSessionAsync(string containerId, CancellationToken cancellationToken = default)
    {
        var container = await FindContainerAsync(containerId, cancellationToken);
        if (container.State != ContainerState.Running)
        {
            throw new InvalidContainerOperationException(containerId, nameof(OpenExecSessionAsync));
        }

        return await _execClient.OpenExecSessionAsync(containerId, cancellationToken);
    }

    public async IAsyncEnumerable<string> FollowContainerLogsAsync(
        string containerId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureContainerExistsAsync(containerId, cancellationToken);
        await foreach (var line in _logClient.FollowContainerLogsAsync(containerId, cancellationToken))
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
        var containers = await _queryClient.ListContainersAsync(cancellationToken);
        foreach (var container in containers)
        {
            if (container.Id == containerId)
            {
                return container;
            }
        }

        throw new ContainerNotFoundException(containerId);
    }

    private static IReadOnlyList<string> TrimAndRemoveEmpty(IReadOnlyList<string> values)
    {
        return values
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .ToList();
    }
}
