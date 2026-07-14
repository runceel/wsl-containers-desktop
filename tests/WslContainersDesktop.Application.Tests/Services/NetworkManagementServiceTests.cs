using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Application.Services;
using WslContainersDesktop.Domain;
using WslContainersDesktop.Application.Tests.Fakes;

namespace WslContainersDesktop.Application.Tests.Services;

[TestClass]
public sealed class NetworkManagementServiceTests
{
    private static DateTimeOffset CreatedAt => new(2026, 7, 4, 9, 0, 0, TimeSpan.Zero);

    private static ContainerNetworkResource CreateNetwork(string name, bool isSystem = false, params string[] connectedContainerNames) => new(
        Name: name,
        Driver: "bridge",
        CreatedAt: CreatedAt,
        ConnectedContainerNames: connectedContainerNames,
        IsSystem: isSystem);

    private static Container CreateContainer(string id) => new(
        Id: id,
        Name: $"name-{id}",
        Image: "nginx:latest",
        State: ContainerState.Running,
        CreatedAt: CreatedAt);

    private static ContainerDetail CreateDetail(string containerId, string containerName, params ContainerNetwork[] networks) => new(
        Id: containerId,
        Name: containerName,
        Image: "nginx:latest",
        State: ContainerState.Running,
        CreatedAt: CreatedAt,
        Command: null,
        Entrypoint: null,
        Ports: [],
        Environment: [],
        Mounts: [],
        Networks: networks,
        RunState: new ContainerRunState(null, null, null, null));

    [TestMethod]
    public async Task GetNetworksAsync_RuntimeReturnsNetworksAndContainersReferenceNetwork_ReturnsNetworkWithConnectedContainerNamesAndUserNetworkIsNotSystem()
    {
        // Arrange
        var client = new FakeRuntimeClients
        {
            Containers = [CreateContainer("c1"), CreateContainer("c2")],
            ContainerDetailsById = new Dictionary<string, ContainerDetail>
            {
                ["c1"] = CreateDetail("c1", "web", new ContainerNetwork("app-net", null)),
                ["c2"] = CreateDetail("c2", "api", new ContainerNetwork("app-net", null), new ContainerNetwork("other", null)),
            },
            Networks = [CreateNetwork("app-net")],
        };
        var sut = new NetworkManagementService(client, client);

        // Act
        var networks = await sut.GetNetworksAsync();

        // Assert
        Assert.HasCount(1, networks);
        Assert.AreEqual("app-net", networks[0].Name);
        CollectionAssert.AreEqual(new[] { "api", "web" }, networks[0].ConnectedContainerNames.ToList());
        Assert.AreEqual(2, networks[0].ConnectedContainerCount);
        Assert.IsFalse(networks[0].IsSystem);
        Assert.IsFalse(networks[0].CanDelete);
    }

    [TestMethod]
    public async Task GetNetworksAsync_ContainerDetailFails_ReturnsNetworksWithoutThrowing()
    {
        // Arrange
        var client = new FakeRuntimeClients
        {
            Containers = [CreateContainer("c1")],
            GetContainerDetailException = new ContainerRuntimeException("detail", 1, "detail failed"),
            Networks = [CreateNetwork("app-net")],
        };
        var sut = new NetworkManagementService(client, client);

        // Act
        var networks = await sut.GetNetworksAsync();

        // Assert
        Assert.HasCount(1, networks);
        Assert.IsEmpty(networks[0].ConnectedContainerNames);
    }

    [TestMethod]
    public async Task GetNetworksAsync_SameContainerReferencesNetworkTwice_ReferencesContainerOnce()
    {
        // Arrange
        var client = new FakeRuntimeClients
        {
            Containers = [CreateContainer("c1")],
            ContainerDetailsById = new Dictionary<string, ContainerDetail>
            {
                ["c1"] = CreateDetail("c1", "web", new ContainerNetwork("app-net", null), new ContainerNetwork("app-net", null)),
            },
            Networks = [CreateNetwork("app-net")],
        };
        var sut = new NetworkManagementService(client, client);

        // Act
        var networks = await sut.GetNetworksAsync();

        // Assert
        CollectionAssert.AreEqual(new[] { "web" }, networks[0].ConnectedContainerNames.ToList());
    }

    [DataTestMethod]
    [DataRow("bridge")]
    [DataRow("host")]
    [DataRow("none")]
    public async Task GetNetworksAsync_RuntimeReturnsReservedNames_MarksSystemNetworks(string reservedName)
    {
        // Arrange
        var client = new FakeRuntimeClients
        {
            Networks = [CreateNetwork(reservedName)],
        };
        var sut = new NetworkManagementService(client, client);

        // Act
        var networks = await sut.GetNetworksAsync();

        // Assert
        Assert.HasCount(1, networks);
        Assert.IsTrue(networks[0].IsSystem);
        Assert.IsFalse(networks[0].CanDelete);
    }

    [TestMethod]
    public async Task CreateAsync_NameHasWhitespace_TrimsAndCallsClientCreateNetwork()
    {
        // Arrange
        var client = new FakeRuntimeClients();
        var sut = new NetworkManagementService(client, client);

        // Act
        await sut.CreateAsync("  app-net  ");

        // Assert
        CollectionAssert.AreEqual(new[] { "app-net" }, client.CreateNetworkCalls);
    }

    [TestMethod]
    public async Task CreateAsync_NameIsWhitespace_ThrowsArgumentExceptionAndDoesNotCallClient()
    {
        // Arrange
        var client = new FakeRuntimeClients();
        var sut = new NetworkManagementService(client, client);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => sut.CreateAsync("   "));
        Assert.IsEmpty(client.CreateNetworkCalls);
    }

    [TestMethod]
    public async Task DeleteAsync_UnusedUserNetwork_CallsClientDeleteNetwork()
    {
        // Arrange
        var client = new FakeRuntimeClients
        {
            Networks = [CreateNetwork("app-net")],
        };
        var sut = new NetworkManagementService(client, client);

        // Act
        await sut.DeleteAsync("app-net");

        // Assert
        CollectionAssert.AreEqual(new[] { "app-net" }, client.DeleteNetworkCalls);
    }

    [TestMethod]
    public async Task DeleteAsync_ConnectedNetwork_ThrowsNetworkInUseExceptionAndDoesNotCallClient()
    {
        // Arrange
        var client = new FakeRuntimeClients
        {
            Containers = [CreateContainer("c1")],
            ContainerDetailsById = new Dictionary<string, ContainerDetail>
            {
                ["c1"] = CreateDetail("c1", "web", new ContainerNetwork("app-net", null)),
            },
            Networks = [CreateNetwork("app-net")],
        };
        var sut = new NetworkManagementService(client, client);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<NetworkInUseException>(() => sut.DeleteAsync("app-net"));
        Assert.AreEqual("app-net", ex.NetworkName);
        CollectionAssert.AreEqual(new[] { "web" }, ex.ConnectedContainerNames.ToList());
        Assert.IsEmpty(client.DeleteNetworkCalls);
    }

    [DataTestMethod]
    [DataRow("bridge")]
    [DataRow("host")]
    [DataRow("none")]
    public async Task DeleteAsync_SystemNetwork_ThrowsSystemNetworkDeletionExceptionAndDoesNotCallClient(string reservedName)
    {
        // Arrange
        var client = new FakeRuntimeClients
        {
            Networks = [CreateNetwork(reservedName, isSystem: true)],
        };
        var sut = new NetworkManagementService(client, client);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<SystemNetworkDeletionException>(() => sut.DeleteAsync(reservedName));
        Assert.AreEqual(reservedName, ex.NetworkName);
        Assert.IsEmpty(client.DeleteNetworkCalls);
    }
}
