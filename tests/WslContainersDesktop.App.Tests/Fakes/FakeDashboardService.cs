using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Application.Services;

namespace WslContainersDesktop_App_Tests.Fakes;

internal sealed class FakeDashboardService : IDashboardService
{
    public DashboardSnapshot? Snapshot { get; set; }

    public Func<CancellationToken, Task<DashboardSnapshot>>? GetSnapshotAsyncFunc { get; set; }

    public int GetSnapshotCallCount { get; private set; }

    public Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        GetSnapshotCallCount++;
        if (GetSnapshotAsyncFunc is not null)
        {
            return GetSnapshotAsyncFunc(cancellationToken);
        }

        return Task.FromResult(Snapshot!);
    }
}
