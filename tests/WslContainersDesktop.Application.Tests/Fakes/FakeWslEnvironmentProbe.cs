using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Tests.Fakes;

/// <summary>
/// <see cref="IWslEnvironmentProbe"/> のテスト用フェイク実装。
/// </summary>
internal sealed class FakeWslEnvironmentProbe : IWslEnvironmentProbe
{
    public WslEnvironmentInfo Info { get; set; } = new(null, false);

    public Exception? GetEnvironmentInfoException { get; set; }

    public int GetEnvironmentInfoCalls { get; private set; }

    public Task<WslEnvironmentInfo> GetEnvironmentInfoAsync(CancellationToken cancellationToken = default)
    {
        GetEnvironmentInfoCalls++;
        if (GetEnvironmentInfoException is not null)
        {
            throw GetEnvironmentInfoException;
        }

        return Task.FromResult(Info);
    }
}
