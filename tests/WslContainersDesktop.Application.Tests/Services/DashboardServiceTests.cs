using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Application.Services;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Tests.Services;

[TestClass]
public sealed class DashboardServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 2, 9, 0, 0, TimeSpan.Zero);

    private static Container CreateContainer(string id) => new(
        Id: id,
        Name: $"name-{id}",
        Image: "nginx:latest",
        State: ContainerState.Running,
        CreatedAt: Now);

    private sealed class FakeDashboardRuntimeClients :
        IContainerQueryClient,
        IImageRuntimeClient,
        IVolumeRuntimeClient,
        INetworkRuntimeClient,
        IContainerStatsClient
    {
        public IReadOnlyList<Container> Containers { get; set; } = [];

        public IReadOnlyList<ContainerImage> Images { get; set; } = [];

        public IReadOnlyList<ContainerVolume> Volumes { get; set; } = [];

        public IReadOnlyList<ContainerNetworkResource> Networks { get; set; } = [];

        public IReadOnlyList<ContainerResourceUsage> Stats { get; set; } = [];

        public Dictionary<string, ContainerDetail> DetailsById { get; set; } = [];

        public Exception? ListContainersException { get; set; }

        public Exception? ListImagesException { get; set; }

        public Exception? ListVolumesException { get; set; }

        public Exception? ListNetworksException { get; set; }

        public Exception? GetContainerStatsException { get; set; }

        public Exception? GetContainerDetailException { get; set; }

        public Func<string, CancellationToken, Task<ContainerDetail>>? GetContainerDetailAsyncFunc { get; set; }

        public int ListContainersCallCount { get; private set; }

        public int ListImagesCallCount { get; private set; }

        public int ListVolumesCallCount { get; private set; }

        public int ListNetworksCallCount { get; private set; }

        public int GetContainerStatsCallCount { get; private set; }

        public int GetContainerDetailCallCount { get; private set; }

        public List<string> GetContainerDetailCalls { get; } = [];

        public Task<IReadOnlyList<Container>> ListContainersAsync(CancellationToken cancellationToken = default)
        {
            ListContainersCallCount++;
            if (ListContainersException is not null)
            {
                throw ListContainersException;
            }

            return Task.FromResult(Containers);
        }

        public Task<IReadOnlyList<ContainerImage>> ListImagesAsync(CancellationToken cancellationToken = default)
        {
            ListImagesCallCount++;
            if (ListImagesException is not null)
            {
                throw ListImagesException;
            }

            return Task.FromResult(Images);
        }

        public Task PullImageAsync(string imageReference, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task DeleteImageAsync(string imageId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IReadOnlyList<ContainerVolume>> ListVolumesAsync(CancellationToken cancellationToken = default)
        {
            ListVolumesCallCount++;
            if (ListVolumesException is not null)
            {
                throw ListVolumesException;
            }

            return Task.FromResult(Volumes);
        }

        public Task CreateVolumeAsync(string name, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task DeleteVolumeAsync(string name, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IReadOnlyList<ContainerNetworkResource>> ListNetworksAsync(CancellationToken cancellationToken = default)
        {
            ListNetworksCallCount++;
            if (ListNetworksException is not null)
            {
                throw ListNetworksException;
            }

            return Task.FromResult(Networks);
        }

        public Task CreateNetworkAsync(string name, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task DeleteNetworkAsync(string name, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<ContainerDetail> GetContainerDetailAsync(string containerId, CancellationToken cancellationToken = default)
        {
            GetContainerDetailCallCount++;
            GetContainerDetailCalls.Add(containerId);
            if (GetContainerDetailException is not null)
            {
                throw GetContainerDetailException;
            }

            if (GetContainerDetailAsyncFunc is not null)
            {
                return GetContainerDetailAsyncFunc(containerId, cancellationToken);
            }

            return Task.FromResult(DetailsById[containerId]);
        }

        public Task<IReadOnlyList<ContainerResourceUsage>> GetContainerStatsAsync(CancellationToken cancellationToken = default)
        {
            GetContainerStatsCallCount++;
            if (GetContainerStatsException is not null)
            {
                throw GetContainerStatsException;
            }

            return Task.FromResult(Stats);
        }
    }

    [TestMethod]
    public async Task GetSnapshotAsync_ContainerDetailsAreSharedForVolumeAndNetworkEnrichment_ReturnsEnrichedSnapshot()
    {
        // Arrange
        var client = new FakeDashboardRuntimeClients
        {
            Containers = [CreateContainer("c1"), CreateContainer("c2")],
            Images = [new ContainerImage("i1", "repo", "latest", 1, Now)],
            Volumes = [new ContainerVolume("vol-1", "local", Now, [])],
            Networks = [new ContainerNetworkResource("net-1", "bridge", Now, [], false)],
            Stats = [new ContainerResourceUsage("c1", "app", 1.0, 1024, 2048)],
            DetailsById = new Dictionary<string, ContainerDetail>
            {
                ["c1"] = new ContainerDetail(
                    Id: "c1",
                    Name: "name-c1",
                    Image: "img",
                    State: ContainerState.Running,
                    CreatedAt: Now,
                    Command: null,
                    Entrypoint: null,
                    Ports: [],
                    Environment: [],
                    Mounts: [new ContainerMount("volume", "vol-1", "/data", false)],
                    Networks: [new ContainerNetwork("net-1", null)],
                    RunState: new ContainerRunState(null, null, null, null)),
                ["c2"] = new ContainerDetail(
                    Id: "c2",
                    Name: "name-c2",
                    Image: "img",
                    State: ContainerState.Running,
                    CreatedAt: Now,
                    Command: null,
                    Entrypoint: null,
                    Ports: [],
                    Environment: [],
                    Mounts: [],
                    Networks: [],
                    RunState: new ContainerRunState(null, null, null, null)),
            },
        };
        var sut = new DashboardService(client, client, client, client, client);

        // Act
        var snapshot = await sut.GetSnapshotAsync();

        // Assert
        Assert.AreEqual(2, client.GetContainerDetailCallCount);
        CollectionAssert.AreEqual(new[] { "c1", "c2" }, client.GetContainerDetailCalls);
        Assert.IsNull(snapshot.Volumes.Exception);
        Assert.IsNull(snapshot.Networks.Exception);
        Assert.AreEqual(1, snapshot.Volumes.Value.Count);
        CollectionAssert.AreEqual(new[] { "name-c1" }, snapshot.Volumes.Value[0].ReferencingContainerNames.ToList());
        Assert.AreEqual(1, snapshot.Networks.Value.Count);
        CollectionAssert.AreEqual(new[] { "name-c1" }, snapshot.Networks.Value[0].ConnectedContainerNames.ToList());
    }

    [TestMethod]
    public async Task GetSnapshotAsync_RootPartialFailure_StoresSectionExceptionAndKeepsOtherSections()
    {
        // Arrange
        var client = new FakeDashboardRuntimeClients
        {
            Containers = [],
            Images = [],
            Volumes = [],
            Networks = [],
            Stats = [],
            ListImagesException = new ContainerRuntimeException("images", 1, "images boom"),
        };
        var sut = new DashboardService(client, client, client, client, client);

        // Act
        var snapshot = await sut.GetSnapshotAsync();

        // Assert
        Assert.IsNotNull(snapshot.Images.Exception);
        Assert.AreEqual("images boom", snapshot.Images.Exception!.Message);
        Assert.IsNotNull(snapshot.Containers.Value);
        Assert.IsNotNull(snapshot.Stats.Value);
        Assert.AreEqual(1, client.ListContainersCallCount);
        Assert.AreEqual(1, client.ListImagesCallCount);
        Assert.AreEqual(1, client.ListVolumesCallCount);
        Assert.AreEqual(1, client.ListNetworksCallCount);
        Assert.AreEqual(1, client.GetContainerStatsCallCount);
    }

    [TestMethod]
    public async Task GetSnapshotAsync_ContainerListFails_DoesNotInspectDetailsAndKeepsVolumeAndNetworkResults()
    {
        // Arrange
        var client = new FakeDashboardRuntimeClients
        {
            Volumes = [new ContainerVolume("vol-1", "local", Now, [])],
            Networks = [new ContainerNetworkResource("net-1", "bridge", Now, [], false)],
            ListContainersException = new InvalidOperationException("containers boom"),
        };
        var sut = new DashboardService(client, client, client, client, client);

        // Act
        var snapshot = await sut.GetSnapshotAsync();

        // Assert
        Assert.IsNotNull(snapshot.Containers.Exception);
        Assert.AreEqual(1, snapshot.Volumes.Value.Count);
        Assert.AreEqual(1, snapshot.Networks.Value.Count);
        Assert.AreEqual(0, client.GetContainerDetailCallCount);
    }

    [TestMethod]
    public async Task GetSnapshotAsync_PartialContainerDetailFailure_SkipsTheFailureAndKeepsOtherData()
    {
        // Arrange
        var client = new FakeDashboardRuntimeClients
        {
            Containers = [CreateContainer("c1"), CreateContainer("c2")],
            Images = [],
            Volumes = [new ContainerVolume("vol-1", "local", Now, [])],
            Networks = [new ContainerNetworkResource("net-1", "bridge", Now, [], false)],
            Stats = [],
            GetContainerDetailAsyncFunc = (containerId, _) =>
            {
                if (containerId == "c2")
                {
                    throw new ContainerRuntimeException("detail", 1, "detail boom");
                }

                return Task.FromResult(new ContainerDetail(
                    Id: containerId,
                    Name: $"name-{containerId}",
                    Image: "img",
                    State: ContainerState.Running,
                    CreatedAt: Now,
                    Command: null,
                    Entrypoint: null,
                    Ports: [],
                    Environment: [],
                    Mounts: [new ContainerMount("volume", "vol-1", "/data", false)],
                    Networks: [new ContainerNetwork("net-1", null)],
                    RunState: new ContainerRunState(null, null, null, null)));
            },
        };
        var sut = new DashboardService(client, client, client, client, client);

        // Act
        var snapshot = await sut.GetSnapshotAsync();

        // Assert
        Assert.IsNull(snapshot.Volumes.Exception);
        Assert.IsNull(snapshot.Networks.Exception);
        Assert.AreEqual(1, snapshot.Volumes.Value.Count);
        CollectionAssert.AreEqual(new[] { "name-c1" }, snapshot.Volumes.Value[0].ReferencingContainerNames.ToList());
        CollectionAssert.AreEqual(new[] { "name-c1" }, snapshot.Networks.Value[0].ConnectedContainerNames.ToList());
    }

    [TestMethod]
    public async Task GetSnapshotAsync_GenericContainerDetailFailure_DoesNotThrowAndReturnsRawVolumeAndNetworkValues()
    {
        // Arrange
        var client = new FakeDashboardRuntimeClients
        {
            Containers = [CreateContainer("c1")],
            Images = [new ContainerImage("i1", "repo", "latest", 1, Now)],
            Volumes = [new ContainerVolume("vol-1", "local", Now, [])],
            Networks = [new ContainerNetworkResource("net-1", "bridge", Now, [], false)],
            Stats = [new ContainerResourceUsage("c1", "app", 1.0, 1024, 2048)],
            GetContainerDetailAsyncFunc = (_, _) => throw new InvalidOperationException("detail boom"),
        };
        var sut = new DashboardService(client, client, client, client, client);

        // Act
        var snapshot = await sut.GetSnapshotAsync();

        // Assert
        Assert.IsNull(snapshot.Containers.Exception);
        Assert.IsNull(snapshot.Images.Exception);
        Assert.IsNull(snapshot.Volumes.Exception);
        Assert.IsNull(snapshot.Networks.Exception);
        Assert.IsNull(snapshot.Stats.Exception);
        Assert.AreEqual(1, snapshot.Volumes.Value.Count);
        Assert.AreEqual(0, snapshot.Volumes.Value[0].ReferencingContainerNames.Count);
        Assert.AreEqual(1, snapshot.Networks.Value.Count);
        Assert.AreEqual(0, snapshot.Networks.Value[0].ConnectedContainerNames.Count);
    }

    [TestMethod]
    public async Task GetSnapshotAsync_NetworkListFailure_StillEnrichesVolumesFromSharedDetails()
    {
        // Arrange
        var client = new FakeDashboardRuntimeClients
        {
            Containers = [CreateContainer("c1")],
            Images = [],
            Volumes = [new ContainerVolume("vol-1", "local", Now, [])],
            Networks = [],
            Stats = [],
            ListNetworksException = new ContainerRuntimeException("networks", 1, "networks boom"),
            DetailsById = new Dictionary<string, ContainerDetail>
            {
                ["c1"] = new ContainerDetail(
                    Id: "c1",
                    Name: "name-c1",
                    Image: "img",
                    State: ContainerState.Running,
                    CreatedAt: Now,
                    Command: null,
                    Entrypoint: null,
                    Ports: [],
                    Environment: [],
                    Mounts: [new ContainerMount("volume", "vol-1", "/data", false)],
                    Networks: [],
                    RunState: new ContainerRunState(null, null, null, null)),
            },
        };
        var sut = new DashboardService(client, client, client, client, client);

        // Act
        var snapshot = await sut.GetSnapshotAsync();

        // Assert
        Assert.IsNull(snapshot.Containers.Exception);
        Assert.IsNull(snapshot.Volumes.Exception);
        Assert.IsNotNull(snapshot.Networks.Exception);
        Assert.AreEqual("networks boom", snapshot.Networks.Exception!.Message);
        Assert.AreEqual(1, snapshot.Volumes.Value.Count);
        CollectionAssert.AreEqual(new[] { "name-c1" }, snapshot.Volumes.Value[0].ReferencingContainerNames.ToList());
    }

    [TestMethod]
    public async Task GetSnapshotAsync_VolumeListFailure_StillEnrichesNetworksFromSharedDetails()
    {
        // Arrange
        var client = new FakeDashboardRuntimeClients
        {
            Containers = [CreateContainer("c1")],
            Images = [],
            Volumes = [],
            Networks = [new ContainerNetworkResource("net-1", "bridge", Now, [], false)],
            Stats = [],
            ListVolumesException = new ContainerRuntimeException("volumes", 1, "volumes boom"),
            DetailsById = new Dictionary<string, ContainerDetail>
            {
                ["c1"] = new ContainerDetail(
                    Id: "c1",
                    Name: "name-c1",
                    Image: "img",
                    State: ContainerState.Running,
                    CreatedAt: Now,
                    Command: null,
                    Entrypoint: null,
                    Ports: [],
                    Environment: [],
                    Mounts: [],
                    Networks: [new ContainerNetwork("net-1", null)],
                    RunState: new ContainerRunState(null, null, null, null)),
            },
        };
        var sut = new DashboardService(client, client, client, client, client);

        // Act
        var snapshot = await sut.GetSnapshotAsync();

        // Assert
        Assert.IsNull(snapshot.Containers.Exception);
        Assert.IsNull(snapshot.Networks.Exception);
        Assert.IsNotNull(snapshot.Volumes.Exception);
        Assert.AreEqual("volumes boom", snapshot.Volumes.Exception!.Message);
        Assert.AreEqual(1, snapshot.Networks.Value.Count);
        CollectionAssert.AreEqual(new[] { "name-c1" }, snapshot.Networks.Value[0].ConnectedContainerNames.ToList());
    }

    [TestMethod]
    public async Task GetSnapshotAsync_OperationCanceledExceptionIsPropagated()
    {
        // Arrange
        var client = new FakeDashboardRuntimeClients
        {
            Containers = [CreateContainer("c1")],
            Images = [],
            Volumes = [],
            Networks = [],
            Stats = [],
            GetContainerDetailAsyncFunc = (_, _) => throw new OperationCanceledException("cancelled"),
        };
        var sut = new DashboardService(client, client, client, client, client);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => sut.GetSnapshotAsync());
    }
}
