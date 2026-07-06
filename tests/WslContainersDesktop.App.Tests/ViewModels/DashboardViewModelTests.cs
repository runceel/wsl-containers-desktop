using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Domain;
using WslContainersDesktop_App.Navigation;
using WslContainersDesktop_App.ViewModels;
using WslContainersDesktop_App_Tests.Fakes;

namespace WslContainersDesktop_App_Tests.ViewModels;

[TestClass]
public sealed class DashboardViewModelTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private sealed record Harness(
        FakeContainerManagementService Container,
        FakeImageManagementService Image,
        FakeVolumeManagementService Volume,
        FakeNetworkManagementService Network,
        NavigationViewModel Navigation,
        ContainersViewModel Containers,
        DashboardViewModel Sut);

    private static Harness CreateHarness(NavigationPageKey initialKey = NavigationPageKey.Settings)
    {
        var container = new FakeContainerManagementService();
        var image = new FakeImageManagementService();
        var volume = new FakeVolumeManagementService();
        var network = new FakeNetworkManagementService();
        var navigation = new NavigationViewModel(initialKey);
        var containers = new ContainersViewModel(container);
        var sut = new DashboardViewModel(container, image, volume, network, navigation, containers);
        return new Harness(container, image, volume, network, navigation, containers, sut);
    }

    private static void SeedAllSucceed(Harness h)
    {
        h.Container.DefaultContainers =
        [
            new Container("c1", "web", "img", ContainerState.Running, Now),
            new Container("c2", "api", "img", ContainerState.Running, Now),
            new Container("c3", "db", "img", ContainerState.Stopped, Now),
        ];
        h.Image.DefaultImages =
        [
            new ContainerImage("i1", "repo", "1", 1, Now),
            new ContainerImage("i2", "repo", "2", 1, Now),
            new ContainerImage("i3", "repo", "3", 1, Now),
        ];
        h.Volume.DefaultVolumes =
        [
            new ContainerVolume("v1", "local", Now, []),
            new ContainerVolume("v2", "local", Now, []),
        ];
        h.Network.DefaultNetworks =
        [
            new ContainerNetworkResource("n0", "bridge", Now, [], true),
            new ContainerNetworkResource("n1", "bridge", Now, [], false),
            new ContainerNetworkResource("n2", "bridge", Now, [], false),
            new ContainerNetworkResource("n3", "bridge", Now, [], false),
        ];
    }

    [TestMethod]
    public async Task RefreshAsync_AllSourcesSucceed_SetsAllCounts()
    {
        var h = CreateHarness();
        SeedAllSucceed(h);

        await h.Sut.RefreshCommand.ExecuteAsync(null);

        Assert.AreEqual(2, h.Sut.RunningContainerCount);
        Assert.AreEqual(1, h.Sut.StoppedContainerCount);
        Assert.AreEqual(3, h.Sut.ImageCount);
        Assert.AreEqual(2, h.Sut.VolumeCount);
        Assert.AreEqual(4, h.Sut.NetworkCount);
        Assert.IsNull(h.Sut.ContainerCountErrorMessage);
        Assert.IsNull(h.Sut.ImageCountErrorMessage);
        Assert.IsNull(h.Sut.VolumeCountErrorMessage);
        Assert.IsNull(h.Sut.NetworkCountErrorMessage);
        Assert.IsFalse(h.Sut.IsContainerCountErrorVisible);
        Assert.IsFalse(h.Sut.IsImageCountErrorVisible);
        Assert.IsFalse(h.Sut.IsVolumeCountErrorVisible);
        Assert.IsFalse(h.Sut.IsNetworkCountErrorVisible);
    }

    [TestMethod]
    public async Task RefreshAsync_NetworksIncludeSystem_NetworkCountCountsAll()
    {
        var h = CreateHarness();
        SeedAllSucceed(h);

        await h.Sut.RefreshCommand.ExecuteAsync(null);

        Assert.AreEqual(4, h.Sut.NetworkCount);
    }

    [TestMethod]
    public async Task RefreshAsync_ImageFetchFails_OtherCountsStillSet()
    {
        var h = CreateHarness();
        SeedAllSucceed(h);
        h.Image.GetImagesException = new InvalidOperationException("img boom");

        await h.Sut.RefreshCommand.ExecuteAsync(null);

        Assert.IsNull(h.Sut.ImageCount);
        Assert.AreEqual("img boom", h.Sut.ImageCountErrorMessage);
        Assert.IsTrue(h.Sut.IsImageCountErrorVisible);
        Assert.AreEqual(2, h.Sut.RunningContainerCount);
        Assert.AreEqual(2, h.Sut.VolumeCount);
        Assert.AreEqual(4, h.Sut.NetworkCount);
        Assert.IsNull(h.Sut.ContainerCountErrorMessage);
        Assert.IsNull(h.Sut.VolumeCountErrorMessage);
        Assert.IsNull(h.Sut.NetworkCountErrorMessage);
    }

    [TestMethod]
    public async Task RefreshAsync_ContainerFetchFails_ContainerCountsNullWithErrorOthersSet()
    {
        var h = CreateHarness();
        SeedAllSucceed(h);
        h.Container.GetContainersResults.Enqueue(
            () => Task.FromException<IReadOnlyList<Container>>(new InvalidOperationException("c boom")));

        await h.Sut.RefreshCommand.ExecuteAsync(null);

        Assert.IsNull(h.Sut.RunningContainerCount);
        Assert.IsNull(h.Sut.StoppedContainerCount);
        Assert.AreEqual("c boom", h.Sut.ContainerCountErrorMessage);
        Assert.IsTrue(h.Sut.IsContainerCountErrorVisible);
        Assert.AreEqual(3, h.Sut.ImageCount);
        Assert.AreEqual(2, h.Sut.VolumeCount);
        Assert.AreEqual(4, h.Sut.NetworkCount);
    }

    [TestMethod]
    public async Task RefreshAsync_VolumeFetchFails_OtherCountsStillSet()
    {
        var h = CreateHarness();
        SeedAllSucceed(h);
        h.Volume.GetVolumesException = new InvalidOperationException("vol boom");

        await h.Sut.RefreshCommand.ExecuteAsync(null);

        Assert.IsNull(h.Sut.VolumeCount);
        Assert.AreEqual("vol boom", h.Sut.VolumeCountErrorMessage);
        Assert.IsTrue(h.Sut.IsVolumeCountErrorVisible);
        Assert.AreEqual(2, h.Sut.RunningContainerCount);
        Assert.AreEqual(3, h.Sut.ImageCount);
        Assert.AreEqual(4, h.Sut.NetworkCount);
        Assert.IsNull(h.Sut.ContainerCountErrorMessage);
        Assert.IsNull(h.Sut.ImageCountErrorMessage);
        Assert.IsNull(h.Sut.NetworkCountErrorMessage);
    }

    [TestMethod]
    public async Task RefreshAsync_NetworkFetchFails_OtherCountsStillSet()
    {
        var h = CreateHarness();
        SeedAllSucceed(h);
        h.Network.GetNetworksException = new InvalidOperationException("net boom");

        await h.Sut.RefreshCommand.ExecuteAsync(null);

        Assert.IsNull(h.Sut.NetworkCount);
        Assert.AreEqual("net boom", h.Sut.NetworkCountErrorMessage);
        Assert.IsTrue(h.Sut.IsNetworkCountErrorVisible);
        Assert.AreEqual(2, h.Sut.RunningContainerCount);
        Assert.AreEqual(3, h.Sut.ImageCount);
        Assert.AreEqual(2, h.Sut.VolumeCount);
        Assert.IsNull(h.Sut.ContainerCountErrorMessage);
        Assert.IsNull(h.Sut.ImageCountErrorMessage);
        Assert.IsNull(h.Sut.VolumeCountErrorMessage);
    }

    [TestMethod]
    public async Task RefreshAsync_StatsSucceed_PopulatesStatsRows()
    {
        var h = CreateHarness();
        SeedAllSucceed(h);
        h.Container.Stats =
        [
            new ContainerResourceUsage("c1", "web", 1.0, 1L, 2L),
            new ContainerResourceUsage("c2", "api", 2.0, 1L, 2L),
        ];

        await h.Sut.RefreshCommand.ExecuteAsync(null);

        Assert.HasCount(2, h.Sut.ContainerStats);
        Assert.AreEqual("c1", h.Sut.ContainerStats[0].ContainerId);
        Assert.IsNull(h.Sut.StatsErrorMessage);
        Assert.IsFalse(h.Sut.IsStatsEmpty);
    }

    [TestMethod]
    public async Task RefreshAsync_StatsReturnsEmpty_ShowsEmptyState()
    {
        var h = CreateHarness();

        await h.Sut.RefreshCommand.ExecuteAsync(null);

        Assert.IsEmpty(h.Sut.ContainerStats);
        Assert.IsTrue(h.Sut.IsStatsEmpty);
        Assert.IsNull(h.Sut.StatsErrorMessage);
        Assert.IsFalse(h.Sut.IsStatsErrorVisible);
    }

    [TestMethod]
    public async Task RefreshAsync_StatsFetchFails_SetsStatsErrorMessage()
    {
        var h = CreateHarness();
        h.Container.GetStatsException = new ContainerRuntimeException("stats", 1, "stats boom");

        await h.Sut.RefreshCommand.ExecuteAsync(null);

        Assert.IsEmpty(h.Sut.ContainerStats);
        Assert.AreEqual("stats boom", h.Sut.StatsErrorMessage);
        Assert.IsTrue(h.Sut.IsStatsErrorVisible);
        Assert.IsFalse(h.Sut.IsStatsEmpty);
    }

    [TestMethod]
    public async Task RefreshAsync_StatsFetchFails_CountsStillSet()
    {
        var h = CreateHarness();
        SeedAllSucceed(h);
        h.Container.GetStatsException = new ContainerRuntimeException("stats", 1, "stats boom");

        await h.Sut.RefreshCommand.ExecuteAsync(null);

        Assert.AreEqual(2, h.Sut.RunningContainerCount);
        Assert.AreEqual(3, h.Sut.ImageCount);
        Assert.AreEqual(2, h.Sut.VolumeCount);
        Assert.AreEqual(4, h.Sut.NetworkCount);
    }

    [TestMethod]
    public async Task RefreshAsync_AllSourcesReturnEmpty_SetsAllCountsToZero()
    {
        var h = CreateHarness();

        await h.Sut.RefreshCommand.ExecuteAsync(null);

        Assert.AreEqual(0, h.Sut.RunningContainerCount);
        Assert.AreEqual(0, h.Sut.StoppedContainerCount);
        Assert.AreEqual(0, h.Sut.ImageCount);
        Assert.AreEqual(0, h.Sut.VolumeCount);
        Assert.AreEqual(0, h.Sut.NetworkCount);
        Assert.IsNull(h.Sut.ContainerCountErrorMessage);
        Assert.IsNull(h.Sut.ImageCountErrorMessage);
        Assert.IsNull(h.Sut.VolumeCountErrorMessage);
        Assert.IsNull(h.Sut.NetworkCountErrorMessage);
        Assert.IsTrue(h.Sut.IsStatsEmpty);
    }

    [TestMethod]
    public async Task RefreshAsync_CalledTwiceWithChangedData_ReplacesCountsAndStats()
    {
        var h = CreateHarness();
        h.Image.DefaultImages =
        [
            new ContainerImage("i1", "repo", "1", 1, Now),
            new ContainerImage("i2", "repo", "2", 1, Now),
            new ContainerImage("i3", "repo", "3", 1, Now),
        ];
        h.Volume.DefaultVolumes =
        [
            new ContainerVolume("v1", "local", Now, []),
            new ContainerVolume("v2", "local", Now, []),
        ];
        h.Container.DefaultContainers =
        [
            new Container("c1", "web", "img", ContainerState.Running, Now),
            new Container("c2", "api", "img", ContainerState.Running, Now),
            new Container("c3", "db", "img", ContainerState.Stopped, Now),
        ];
        h.Container.Stats =
        [
            new ContainerResourceUsage("c1", "web", 1.0, 1L, 2L),
            new ContainerResourceUsage("c2", "api", 2.0, 1L, 2L),
        ];

        await h.Sut.RefreshCommand.ExecuteAsync(null);

        h.Image.DefaultImages = [new ContainerImage("i1", "repo", "1", 1, Now)];
        h.Volume.DefaultVolumes = [];
        h.Container.DefaultContainers = [new Container("c1", "web", "img", ContainerState.Running, Now)];
        h.Container.Stats = [new ContainerResourceUsage("c1", "web", 1.0, 1L, 2L)];

        await h.Sut.RefreshCommand.ExecuteAsync(null);

        Assert.AreEqual(1, h.Sut.ImageCount);
        Assert.AreEqual(0, h.Sut.VolumeCount);
        Assert.AreEqual(1, h.Sut.RunningContainerCount);
        Assert.AreEqual(0, h.Sut.StoppedContainerCount);
        Assert.HasCount(1, h.Sut.ContainerStats);
    }

    [TestMethod]
    public async Task RefreshAsync_FailureThenSuccess_ClearsErrorMessage()
    {
        var h = CreateHarness();
        h.Image.GetImagesException = new InvalidOperationException("img boom");

        await h.Sut.RefreshCommand.ExecuteAsync(null);

        h.Image.GetImagesException = null;
        h.Image.DefaultImages =
        [
            new ContainerImage("i1", "repo", "1", 1, Now),
            new ContainerImage("i2", "repo", "2", 1, Now),
        ];

        await h.Sut.RefreshCommand.ExecuteAsync(null);

        Assert.IsNull(h.Sut.ImageCountErrorMessage);
        Assert.IsFalse(h.Sut.IsImageCountErrorVisible);
        Assert.AreEqual(2, h.Sut.ImageCount);
    }

    [TestMethod]
    public async Task RefreshAsync_SuccessThenFailure_ClearsStaleStats()
    {
        var h = CreateHarness();
        h.Container.Stats =
        [
            new ContainerResourceUsage("c1", "web", 1.0, 1L, 2L),
            new ContainerResourceUsage("c2", "api", 2.0, 1L, 2L),
        ];

        await h.Sut.RefreshCommand.ExecuteAsync(null);

        h.Container.GetStatsException = new ContainerRuntimeException("stats", 1, "stats boom");

        await h.Sut.RefreshCommand.ExecuteAsync(null);

        Assert.IsEmpty(h.Sut.ContainerStats);
        Assert.AreEqual("stats boom", h.Sut.StatsErrorMessage);
        Assert.IsTrue(h.Sut.IsStatsErrorVisible);
    }

    [TestMethod]
    public async Task RefreshAsync_CountsMatchListViewModelItemCounts()
    {
        var h = CreateHarness();
        SeedAllSucceed(h);
        var imagesVm = new ImagesViewModel(h.Image);
        var volumesVm = new VolumesViewModel(h.Volume);
        var networksVm = new NetworksViewModel(h.Network);
        var containersVm = h.Containers;

        await h.Sut.RefreshCommand.ExecuteAsync(null);
        await imagesVm.RefreshCommand.ExecuteAsync(null);
        await volumesVm.RefreshCommand.ExecuteAsync(null);
        await networksVm.RefreshCommand.ExecuteAsync(null);
        await containersVm.RefreshCommand.ExecuteAsync(null);

        Assert.AreEqual(imagesVm.Images.Count, h.Sut.ImageCount);
        Assert.AreEqual(volumesVm.Volumes.Count, h.Sut.VolumeCount);
        Assert.AreEqual(networksVm.Networks.Count, h.Sut.NetworkCount);
        Assert.AreEqual(containersVm.Containers.Count, h.Sut.RunningContainerCount + h.Sut.StoppedContainerCount);
    }

    [TestMethod]
    public async Task RefreshAsync_CalledWhileAlreadyRefreshing_SecondCallIsIgnored()
    {
        var h = CreateHarness();
        SeedAllSucceed(h);
        var gate = new TaskCompletionSource<IReadOnlyList<Container>>();
        h.Container.GetContainersResults.Enqueue(() => gate.Task);

        var first = h.Sut.RefreshCommand.ExecuteAsync(null);
        var second = h.Sut.RefreshCommand.ExecuteAsync(null);
        gate.SetResult(h.Container.DefaultContainers);
        await first;
        await second;

        Assert.AreEqual(1, h.Image.GetImagesCallCount);
        Assert.AreEqual(1, h.Volume.GetVolumesCallCount);
        Assert.AreEqual(1, h.Network.GetNetworksCallCount);
    }

    [TestMethod]
    public void ShowContainers_Invoked_NavigatesToContainers()
    {
        var h = CreateHarness(NavigationPageKey.Settings);

        h.Sut.ShowContainersCommand.Execute(null);

        Assert.AreEqual(NavigationPageKey.Containers, h.Navigation.CurrentPageKey);
    }

    [TestMethod]
    public void ShowImages_Invoked_NavigatesToImages()
    {
        var h = CreateHarness(NavigationPageKey.Settings);

        h.Sut.ShowImagesCommand.Execute(null);

        Assert.AreEqual(NavigationPageKey.Images, h.Navigation.CurrentPageKey);
    }

    [TestMethod]
    public void ShowVolumes_Invoked_NavigatesToVolumes()
    {
        var h = CreateHarness(NavigationPageKey.Settings);

        h.Sut.ShowVolumesCommand.Execute(null);

        Assert.AreEqual(NavigationPageKey.Volumes, h.Navigation.CurrentPageKey);
    }

    [TestMethod]
    public void ShowNetworks_Invoked_NavigatesToNetworks()
    {
        var h = CreateHarness(NavigationPageKey.Settings);

        h.Sut.ShowNetworksCommand.Execute(null);

        Assert.AreEqual(NavigationPageKey.Networks, h.Navigation.CurrentPageKey);
    }

    [TestMethod]
    public async Task OpenContainerDetails_Invoked_NavigatesToContainersAndDelegatesToContainersViewModel()
    {
        var h = CreateHarness(NavigationPageKey.Settings);
        h.Container.ContainerDetail = new ContainerDetail(
            "c1", "web", "img", ContainerState.Running, Now, null, null, [], [], [], [],
            new ContainerRunState(null, null, null, null));
        var row = new DashboardContainerStatsRowViewModel(new ContainerResourceUsage("c1", "web", 1.0, 1L, 2L));

        await h.Sut.OpenContainerDetailsCommand.ExecuteAsync(row);

        Assert.AreEqual(NavigationPageKey.Containers, h.Navigation.CurrentPageKey);
        Assert.IsTrue(h.Containers.IsDetailPanelVisible);
        CollectionAssert.Contains(h.Container.GetContainerDetailCalls, "c1");
    }

    [TestMethod]
    public async Task OpenContainerLogs_Invoked_NavigatesToContainersAndDelegatesToContainersViewModel()
    {
        var h = CreateHarness(NavigationPageKey.Settings);
        h.Container.DefaultContainers = [new Container("c1", "web", "img", ContainerState.Stopped, Now)];
        h.Container.DefaultLogs = ["log"];
        var row = new DashboardContainerStatsRowViewModel(new ContainerResourceUsage("c1", "web", 1.0, 1L, 2L));

        await h.Sut.OpenContainerLogsCommand.ExecuteAsync(row);

        Assert.AreEqual(NavigationPageKey.Containers, h.Navigation.CurrentPageKey);
        Assert.IsTrue(h.Containers.IsLogPanelVisible);
        CollectionAssert.Contains(h.Container.GetContainerLogsCalls, "c1");
    }
}
