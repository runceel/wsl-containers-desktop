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

    public List<string> StartCalls { get; } = [];

    public List<string> StopCalls { get; } = [];

    public List<string> DeleteCalls { get; } = [];

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
}
