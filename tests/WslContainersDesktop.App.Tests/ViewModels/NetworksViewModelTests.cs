using System.Collections.Specialized;
using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Domain;
using WslContainersDesktop_App.ViewModels;
using WslContainersDesktop_App_Tests.Fakes;

namespace WslContainersDesktop_App_Tests.ViewModels;

[TestClass]
public sealed class NetworksViewModelTests
{
    private static DateTimeOffset CreatedAt => new(2026, 7, 4, 9, 0, 0, TimeSpan.Zero);

    private static ContainerNetworkResource CreateNetwork(string name, bool isSystem = false, params string[] connectedContainerNames) => new(
        Name: name,
        Driver: "bridge",
        CreatedAt: CreatedAt,
        ConnectedContainerNames: connectedContainerNames,
        IsSystem: isSystem);

    [TestMethod]
    public async Task RefreshAsync_ServiceReturnsUserAndSystemNetworks_PopulatesRowsAndSetsHasNetworksAndNoUserNetworksFlags()
    {
        // Arrange
        var service = new FakeNetworkManagementService { DefaultNetworks = [CreateNetwork("app-net"), CreateNetwork("bridge", true)] };
        var sut = new NetworksViewModel(service);

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.HasCount(2, sut.Networks);
        Assert.IsTrue(sut.HasNetworks);
        Assert.IsFalse(sut.HasNoUserNetworks);
        Assert.IsFalse(sut.IsNoUserNetworksInfoVisible);
        Assert.IsNull(sut.ErrorMessage);
    }

    [TestMethod]
    public async Task RefreshAsync_ServiceReturnsOnlySystemNetworks_SetsNoUserNetworksFlag()
    {
        // Arrange
        var service = new FakeNetworkManagementService { DefaultNetworks = [CreateNetwork("bridge", true)] };
        var sut = new NetworksViewModel(service);

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.HasCount(1, sut.Networks);
        Assert.IsTrue(sut.HasNetworks);
        Assert.IsTrue(sut.HasNoUserNetworks);
        Assert.IsTrue(sut.IsNoUserNetworksInfoVisible);
    }

    [TestMethod]
    public async Task RefreshAsync_ServiceReturnsEmptyList_ClearsNetworksAndSetsNoUserNetworksFlag()
    {
        // Arrange
        var service = new FakeNetworkManagementService { DefaultNetworks = [] };
        var sut = new NetworksViewModel(service);

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.IsEmpty(sut.Networks);
        Assert.IsFalse(sut.HasNetworks);
        Assert.IsTrue(sut.HasNoUserNetworks);
        Assert.IsFalse(sut.IsNoUserNetworksInfoVisible);
    }

    [TestMethod]
    public async Task RefreshAsync_ServiceThrows_ErrorMessageIsSetAndExistingRowsArePreserved()
    {
        // Arrange
        var service = new FakeNetworkManagementService { DefaultNetworks = [CreateNetwork("app-net")] };
        var sut = new NetworksViewModel(service);
        await sut.RefreshAsync();
        service.GetNetworksException = new ContainerRuntimeException("list", 1, "refresh failed");

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.AreEqual("refresh failed", sut.ErrorMessage);
        Assert.HasCount(1, sut.Networks);
        Assert.AreEqual("app-net", sut.Networks[0].Name);
    }

    [TestMethod]
    public async Task CreateAsync_ServiceSucceeds_ClearsInputSetsStatusAndRefreshesNetworks()
    {
        // Arrange
        var service = new FakeNetworkManagementService { CreateResult = CreateNetwork("app-net") };
        var sut = new NetworksViewModel(service) { NewNetworkName = "  app-net  " };

        // Act
        await sut.CreateAsync();

        // Assert
        Assert.AreEqual(string.Empty, sut.NewNetworkName);
        Assert.AreEqual("Network created.", sut.StatusMessage);
        Assert.HasCount(1, sut.Networks);
        Assert.AreEqual("app-net", sut.Networks[0].Name);
        Assert.IsFalse(sut.IsCreating);
    }

    [TestMethod]
    public async Task CreateAsync_NewNetworkNameIsEmpty_ShowsValidationErrorAndDoesNotCallService()
    {
        // Arrange
        var service = new FakeNetworkManagementService();
        var sut = new NetworksViewModel(service) { NewNetworkName = string.Empty };

        // Act
        await sut.CreateAsync();

        // Assert
        Assert.AreEqual("Network name is required.", sut.ErrorMessage);
        Assert.IsFalse(sut.IsCreating);
        Assert.IsEmpty(service.CreateCalls);
    }

    [TestMethod]
    public async Task CreateAsync_ServiceThrows_ErrorMessageIsSetAndInputIsPreserved()
    {
        // Arrange
        var service = new FakeNetworkManagementService { CreateException = new ContainerRuntimeException("create", 1, "create failed") };
        var sut = new NetworksViewModel(service) { NewNetworkName = "app-net" };

        // Act
        await sut.CreateAsync();

        // Assert
        Assert.AreEqual("create failed", sut.ErrorMessage);
        Assert.AreEqual("app-net", sut.NewNetworkName);
        Assert.IsFalse(sut.IsCreating);
    }

    [TestMethod]
    public async Task CreateAsync_RefreshAfterCreateFails_ClearsSuccessStatusAndShowsRefreshError()
    {
        // Arrange
        var service = new FakeNetworkManagementService { CreateResult = CreateNetwork("app-net"), GetNetworksException = new ContainerRuntimeException("list", 1, "refresh failed") };
        var sut = new NetworksViewModel(service) { NewNetworkName = "app-net" };

        // Act
        await sut.CreateAsync();

        // Assert
        Assert.AreEqual("refresh failed", sut.ErrorMessage);
        Assert.IsNull(sut.StatusMessage);
    }

    [TestMethod]
    public async Task CreateAsync_OperationIsRunning_IsCreatingIsTrueThenFalse()
    {
        // Arrange
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakeNetworkManagementService { CreateResult = CreateNetwork("app-net"), CreateGate = gate };
        var sut = new NetworksViewModel(service) { NewNetworkName = "app-net" };

        // Act
        var createTask = sut.CreateAsync();

        // Assert
        Assert.IsTrue(sut.IsCreating);
        gate.SetResult(true);
        await createTask;
        Assert.IsFalse(sut.IsCreating);
    }

    [TestMethod]
    public async Task DeleteAsync_ServiceSucceeds_RemovesNetworkRowAndUpdatesHasNetworkFlags()
    {
        // Arrange
        var service = new FakeNetworkManagementService { DefaultNetworks = [CreateNetwork("app-net")] };
        var sut = new NetworksViewModel(service);
        await sut.RefreshAsync();
        var row = sut.Networks[0];

        // Act
        await sut.DeleteAsync(row);

        // Assert
        Assert.IsEmpty(sut.Networks);
        Assert.IsFalse(sut.HasNetworks);
        Assert.IsTrue(sut.HasNoUserNetworks);
    }

    [TestMethod]
    public async Task DeleteAsync_ServiceThrows_ErrorMessageIsSetAndRowRemains()
    {
        // Arrange
        var service = new FakeNetworkManagementService { DefaultNetworks = [CreateNetwork("app-net")], DeleteException = new ContainerRuntimeException("delete", 1, "delete failed") };
        var sut = new NetworksViewModel(service);
        await sut.RefreshAsync();
        var row = sut.Networks[0];

        // Act
        await sut.DeleteAsync(row);

        // Assert
        Assert.AreEqual("delete failed", sut.ErrorMessage);
        Assert.HasCount(1, sut.Networks);
        Assert.AreSame(row, sut.Networks[0]);
    }

    [TestMethod]
    public async Task DeleteAsync_ConnectedRow_DoesNotCallServiceAndShowsError()
    {
        // Arrange
        var service = new FakeNetworkManagementService { DefaultNetworks = [CreateNetwork("app-net", false, "web")] };
        var sut = new NetworksViewModel(service);
        await sut.RefreshAsync();
        var row = sut.Networks[0];

        // Act
        await sut.DeleteAsync(row);

        // Assert
        Assert.AreEqual("Network 'app-net' is in use by: web", sut.ErrorMessage);
        Assert.IsEmpty(service.DeleteCalls);
        Assert.HasCount(1, sut.Networks);
    }

    [TestMethod]
    public async Task DeleteAsync_SystemRow_DoesNotCallServiceAndShowsError()
    {
        // Arrange
        var service = new FakeNetworkManagementService { DefaultNetworks = [CreateNetwork("bridge", true)] };
        var sut = new NetworksViewModel(service);
        await sut.RefreshAsync();
        var row = sut.Networks[0];

        // Act
        await sut.DeleteAsync(row);

        // Assert
        Assert.AreEqual("Network 'bridge' is a system network and cannot be deleted.", sut.ErrorMessage);
        Assert.IsEmpty(service.DeleteCalls);
        Assert.HasCount(1, sut.Networks);
    }

    [TestMethod]
    public async Task DeleteAsync_NetworkInUseException_UsesExceptionNamesInErrorMessage()
    {
        // Arrange
        var service = new FakeNetworkManagementService { DefaultNetworks = [CreateNetwork("app-net")], DeleteException = new NetworkInUseException("app-net", ["web", "db"]) };
        var sut = new NetworksViewModel(service);
        await sut.RefreshAsync();
        var row = sut.Networks[0];

        // Act
        await sut.DeleteAsync(row);

        // Assert
        Assert.AreEqual("Network 'app-net' is in use by: web, db", sut.ErrorMessage);
        Assert.HasCount(1, sut.Networks);
    }

    [TestMethod]
    public async Task DeleteAsync_SystemNetworkDeletionException_UsesExceptionMessage()
    {
        // Arrange
        var service = new FakeNetworkManagementService { DefaultNetworks = [CreateNetwork("bridge", true)], DeleteException = new SystemNetworkDeletionException("bridge") };
        var sut = new NetworksViewModel(service);
        await sut.RefreshAsync();
        var row = sut.Networks[0];

        // Act
        await sut.DeleteAsync(row);

        // Assert
        Assert.AreEqual("Network 'bridge' is a system network and cannot be deleted.", sut.ErrorMessage);
        Assert.HasCount(1, sut.Networks);
    }

    [TestMethod]
    public async Task RefreshAsync_RefetchesIdenticalNetworks_PreservesRowInstances()
    {
        // Arrange
        var service = new FakeNetworkManagementService { DefaultNetworks = [CreateNetwork("app-net"), CreateNetwork("db-net")] };
        var sut = new NetworksViewModel(service);
        await sut.RefreshAsync();
        var row1 = sut.Networks[0];
        var row2 = sut.Networks[1];

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.HasCount(2, sut.Networks);
        Assert.AreSame(row1, sut.Networks[0]);
        Assert.AreSame(row2, sut.Networks[1]);
    }

    [TestMethod]
    public async Task RefreshAsync_RefetchesIdenticalNetworks_RaisesNoCollectionChanged()
    {
        // Arrange
        var service = new FakeNetworkManagementService { DefaultNetworks = [CreateNetwork("app-net"), CreateNetwork("db-net")] };
        var sut = new NetworksViewModel(service);
        await sut.RefreshAsync();
        var actions = new List<NotifyCollectionChangedAction>();
        sut.Networks.CollectionChanged += (_, e) => actions.Add(e.Action);

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.IsEmpty(actions);
    }

    [TestMethod]
    public async Task RefreshAsync_NetworkRemovedOnServer_RowRemovedAndOthersPreserved()
    {
        // Arrange
        var service = new FakeNetworkManagementService { DefaultNetworks = [CreateNetwork("app-net"), CreateNetwork("db-net")] };
        var sut = new NetworksViewModel(service);
        await sut.RefreshAsync();
        var row1 = sut.Networks[0];
        service.DefaultNetworks = [CreateNetwork("app-net")];

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.HasCount(1, sut.Networks);
        Assert.AreSame(row1, sut.Networks[0]);
    }

    [TestMethod]
    public async Task RefreshAsync_NewNetworkOnServer_RowAddedAndExistingPreserved()
    {
        // Arrange
        var service = new FakeNetworkManagementService { DefaultNetworks = [CreateNetwork("app-net")] };
        var sut = new NetworksViewModel(service);
        await sut.RefreshAsync();
        var row1 = sut.Networks[0];
        service.DefaultNetworks = [CreateNetwork("app-net"), CreateNetwork("db-net")];

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.HasCount(2, sut.Networks);
        Assert.AreSame(row1, sut.Networks[0]);
        Assert.AreEqual("db-net", sut.Networks[1].Name);
    }

    [TestMethod]
    public async Task RefreshAsync_NetworkConnectionsChanged_ChangedRowReplacedWithNewUsageTextAndOthersPreserved()
    {
        // Arrange
        var service = new FakeNetworkManagementService { DefaultNetworks = [CreateNetwork("app-net"), CreateNetwork("db-net", false, "web")] };
        var sut = new NetworksViewModel(service);
        await sut.RefreshAsync();
        var appRow = sut.Networks[0];
        var dbRow = sut.Networks[1];
        service.DefaultNetworks = [CreateNetwork("app-net"), CreateNetwork("db-net", false, "web", "api")];

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.HasCount(2, sut.Networks);
        Assert.AreSame(appRow, sut.Networks[0]);
        Assert.AreNotSame(dbRow, sut.Networks[1]);
        Assert.AreEqual("web, api", sut.Networks[1].UsageText);
    }

    [TestMethod]
    public async Task RefreshAsync_RefetchesEquivalentNetworksFromFreshRecordInstances_PreservesRowInstances()
    {
        // Arrange
        var service = new FakeNetworkManagementService { DefaultNetworks = [CreateNetwork("app-net", false, "web")] };
        var sut = new NetworksViewModel(service);
        await sut.RefreshAsync();
        var row1 = sut.Networks[0];
        service.DefaultNetworks = [CreateNetwork("app-net", false, "web")];

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.AreSame(row1, sut.Networks[0]);
    }
}
