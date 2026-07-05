using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Tests.Fakes;

/// <summary>
/// <see cref="IWslResourceLimitsStore"/> のテスト用フェイク実装。
/// </summary>
internal sealed class FakeWslResourceLimitsStore : IWslResourceLimitsStore
{
    public WslResourceLimits Limits { get; set; } = WslResourceLimits.Defaults;

    public Exception? GetException { get; set; }

    public Exception? SaveException { get; set; }

    public List<WslResourceLimits> SaveCalls { get; } = [];

    public Task<WslResourceLimits> GetAsync(CancellationToken cancellationToken = default)
    {
        if (GetException is not null)
        {
            throw GetException;
        }

        return Task.FromResult(Limits);
    }

    public Task SaveAsync(WslResourceLimits limits, CancellationToken cancellationToken = default)
    {
        SaveCalls.Add(limits);
        if (SaveException is not null)
        {
            throw SaveException;
        }

        Limits = limits;
        return Task.CompletedTask;
    }
}
