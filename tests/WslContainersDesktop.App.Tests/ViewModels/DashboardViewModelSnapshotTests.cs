using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Application.Services;
using WslContainersDesktop.Domain;
using WslContainersDesktop_App.Navigation;
using WslContainersDesktop_App.ViewModels;
using WslContainersDesktop_App_Tests.Fakes;

namespace WslContainersDesktop_App_Tests.ViewModels;

[TestClass]
public sealed class DashboardViewModelSnapshotTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 2, 9, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task RefreshAsync_SnapshotContainsErrors_SetsVisibleErrorMessages()
    {
        // Arrange
        var dashboardService = new FakeDashboardService
        {
            Snapshot = new DashboardSnapshot
            {
                Containers = new DashboardSection<IReadOnlyList<Container>>
                {
                    Value = []
                },
                Images = new DashboardSection<IReadOnlyList<ContainerImage>>
                {
                    Value = [],
                    Exception = new InvalidOperationException("img boom"),
                },
                Volumes = new DashboardSection<IReadOnlyList<ContainerVolume>>
                {
                    Value = [],
                },
                Networks = new DashboardSection<IReadOnlyList<ContainerNetworkResource>>
                {
                    Value = [],
                },
                Stats = new DashboardSection<IReadOnlyList<ContainerResourceUsage>>
                {
                    Value = [],
                },
            },
        };
        var container = new FakeContainerManagementService();
        var navigation = new NavigationViewModel(NavigationPageKey.Settings);
        var containers = new ContainersViewModel(container);
        var sut = new DashboardViewModel(dashboardService, navigation, containers);

        // Act
        await sut.RefreshCommand.ExecuteAsync(null);

        // Assert
        Assert.AreEqual("img boom", sut.ImageCountErrorMessage);
        Assert.IsTrue(sut.IsImageCountErrorVisible);
        Assert.IsNull(sut.ImageCount);
    }

    [TestMethod]
    public async Task RefreshAsync_ReentrantCalls_GetSnapshotCalledOnce()
    {
        // Arrange
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dashboardService = new FakeDashboardService
        {
            Snapshot = new DashboardSnapshot
            {
                Containers = new DashboardSection<IReadOnlyList<Container>> { Value = [] },
                Images = new DashboardSection<IReadOnlyList<ContainerImage>> { Value = [] },
                Volumes = new DashboardSection<IReadOnlyList<ContainerVolume>> { Value = [] },
                Networks = new DashboardSection<IReadOnlyList<ContainerNetworkResource>> { Value = [] },
                Stats = new DashboardSection<IReadOnlyList<ContainerResourceUsage>> { Value = [] },
            },
        };
        dashboardService.GetSnapshotAsyncFunc = async _ =>
        {
            await gate.Task;
            return dashboardService.Snapshot!;
        };
        var container = new FakeContainerManagementService();
        var navigation = new NavigationViewModel(NavigationPageKey.Settings);
        var containers = new ContainersViewModel(container);
        var sut = new DashboardViewModel(dashboardService, navigation, containers);

        // Act
        var first = sut.RefreshCommand.ExecuteAsync(null);
        var second = sut.RefreshCommand.ExecuteAsync(null);
        gate.SetResult(true);
        await first;
        await second;

        // Assert
        Assert.AreEqual(1, dashboardService.GetSnapshotCallCount);
    }
}
