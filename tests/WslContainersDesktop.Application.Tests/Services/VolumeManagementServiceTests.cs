using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Application.Services;
using WslContainersDesktop.Application.Tests.Fakes;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Tests.Services;

[TestClass]
public sealed class VolumeManagementServiceTests
{
    private static Container CreateContainer(string id) => new(
        Id: id,
        Name: $"name-{id}",
        Image: "nginx:latest",
        State: ContainerState.Running,
        CreatedAt: new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero));

    private static ContainerDetail CreateDetail(string containerId, string containerName, params ContainerMount[] mounts) => new(
        Id: containerId,
        Name: containerName,
        Image: "nginx:latest",
        State: ContainerState.Running,
        CreatedAt: new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero),
        Command: null,
        Entrypoint: null,
        Ports: [],
        Environment: [],
        Mounts: mounts,
        Networks: [],
        RunState: new ContainerRunState(null, null, null, null));

    [TestMethod]
    public async Task GetVolumesAsync_RuntimeReturnsVolumesAndInspectableContainerReferencesVolume_ReturnsVolumeWithReferencingContainerName()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero);
        var client = new FakeContainerRuntimeClient
        {
            Containers = [CreateContainer("c1")],
            ContainerDetailsById = new Dictionary<string, ContainerDetail>
            {
                ["c1"] = CreateDetail("c1", "web", new ContainerMount("bind", "/var/lib/docker/volumes/vol-demo/_data", "/data", false)),
            },
            Volumes = [new ContainerVolume("vol-demo", "guest", createdAt, [])],
        };
        var sut = new VolumeManagementService(client);

        // Act
        var volumes = await sut.GetVolumesAsync();

        // Assert
        Assert.HasCount(1, volumes);
        Assert.AreEqual("vol-demo", volumes[0].Name);
        CollectionAssert.AreEqual(new[] { "web" }, volumes[0].ReferencingContainerNames.ToList());
    }

    [TestMethod]
    public async Task GetVolumesAsync_ContainerDetailFails_ReturnsVolumesWithoutThrowing()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero);
        var client = new FakeContainerRuntimeClient
        {
            Containers = [CreateContainer("c1")],
            GetContainerDetailException = new ContainerRuntimeException("detail", 1, "detail failed"),
            Volumes = [new ContainerVolume("vol-demo", "guest", createdAt, [])],
        };
        var sut = new VolumeManagementService(client);

        // Act
        var volumes = await sut.GetVolumesAsync();

        // Assert
        Assert.HasCount(1, volumes);
        Assert.IsEmpty(volumes[0].ReferencingContainerNames);
    }

    [TestMethod]
    public async Task GetVolumesAsync_SameContainerReferencesVolumeTwice_ReferencesContainerOnce()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero);
        var client = new FakeContainerRuntimeClient
        {
            Containers = [CreateContainer("c1")],
            ContainerDetailsById = new Dictionary<string, ContainerDetail>
            {
                ["c1"] = CreateDetail(
                    "c1",
                    "web",
                    new ContainerMount("bind", "/var/lib/docker/volumes/vol-demo/_data", "/data", false),
                    new ContainerMount("bind", "/mnt/vol-demo", "/data2", false)),
            },
            Volumes = [new ContainerVolume("vol-demo", "guest", createdAt, [])],
        };
        var sut = new VolumeManagementService(client);

        // Act
        var volumes = await sut.GetVolumesAsync();

        // Assert
        CollectionAssert.AreEqual(new[] { "web" }, volumes[0].ReferencingContainerNames.ToList());
    }

    [TestMethod]
    public async Task GetVolumesAsync_TwoContainersReferenceSameVolume_ReferencesAreSortedByName()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero);
        var client = new FakeContainerRuntimeClient
        {
            Containers = [CreateContainer("c1"), CreateContainer("c2")],
            ContainerDetailsById = new Dictionary<string, ContainerDetail>
            {
                ["c1"] = CreateDetail("c1", "web", new ContainerMount("bind", "/var/lib/docker/volumes/vol-demo/_data", "/data", false)),
                ["c2"] = CreateDetail("c2", "api", new ContainerMount("bind", "/var/lib/docker/volumes/vol-demo/_data", "/data", false)),
            },
            Volumes = [new ContainerVolume("vol-demo", "guest", createdAt, [])],
        };
        var sut = new VolumeManagementService(client);

        // Act
        var volumes = await sut.GetVolumesAsync();

        // Assert
        CollectionAssert.AreEqual(new[] { "api", "web" }, volumes[0].ReferencingContainerNames.ToList());
    }

    [TestMethod]
    public async Task CreateAsync_NameHasWhitespace_TrimsAndCallsClientCreateVolume()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient();
        var sut = new VolumeManagementService(client);

        // Act
        await sut.CreateAsync("  vol-demo  ");

        // Assert
        CollectionAssert.AreEqual(new[] { "vol-demo" }, client.CreateVolumeCalls);
    }

    [TestMethod]
    public async Task CreateAsync_NameIsWhitespace_ThrowsArgumentExceptionAndDoesNotCallClient()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient();
        var sut = new VolumeManagementService(client);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => sut.CreateAsync("   "));
        Assert.IsEmpty(client.CreateVolumeCalls);
    }

    [TestMethod]
    public async Task DeleteAsync_UnusedVolume_CallsClientDeleteVolume()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient();
        var sut = new VolumeManagementService(client);

        // Act
        await sut.DeleteAsync("vol-demo");

        // Assert
        CollectionAssert.AreEqual(new[] { "vol-demo" }, client.DeleteVolumeCalls);
    }

    [TestMethod]
    public async Task DeleteAsync_InUseVolume_ThrowsVolumeInUseExceptionAndDoesNotCallClient()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient
        {
            Containers = [CreateContainer("c1")],
            ContainerDetailsById = new Dictionary<string, ContainerDetail>
            {
                ["c1"] = CreateDetail("c1", "web", new ContainerMount("bind", "/var/lib/docker/volumes/vol-demo/_data", "/data", false)),
            },
        };
        var sut = new VolumeManagementService(client);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<VolumeInUseException>(() => sut.DeleteAsync("vol-demo"));
        Assert.AreEqual("vol-demo", ex.VolumeName);
        CollectionAssert.AreEqual(new[] { "web" }, ex.ReferencingContainerNames.ToList());
        Assert.IsEmpty(client.DeleteVolumeCalls);
    }

    [TestMethod]
    public async Task DeleteAsync_ClientRejectsInUseVolume_ExceptionPropagates()
    {
        // Arrange
        var runtimeException = new ContainerRuntimeException("delete", 1, "volume is in use");
        var client = new FakeContainerRuntimeClient
        {
            DeleteVolumeException = runtimeException,
        };
        var sut = new VolumeManagementService(client);

        // Act & Assert
        var actual = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.DeleteAsync("vol-demo"));
        Assert.AreSame(runtimeException, actual);
    }
}
