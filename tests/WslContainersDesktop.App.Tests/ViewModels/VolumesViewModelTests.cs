using System.Collections.Specialized;
using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Domain;
using WslContainersDesktop_App.ViewModels;
using WslContainersDesktop_App_Tests.Fakes;

namespace WslContainersDesktop_App_Tests.ViewModels;

[TestClass]
public sealed class VolumesViewModelTests
{
    private static ContainerVolume CreateVolume(string name, params string[] referencingContainerNames) => new(
        Name: name,
        Driver: "guest",
        CreatedAt: new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero),
        ReferencingContainerNames: referencingContainerNames);

    [TestMethod]
    public async Task RefreshAsync_ServiceReturnsVolumes_PopulatesRowsAndClearsErrorAndIsEmptyIsFalse()
    {
        // Arrange
        var service = new FakeVolumeManagementService
        {
            DefaultVolumes = [CreateVolume("vol-1"), CreateVolume("vol-2")],
        };
        var sut = new VolumesViewModel(service);

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.HasCount(2, sut.Volumes);
        Assert.AreEqual("vol-1", sut.Volumes[0].Name);
        Assert.IsFalse(sut.IsEmpty);
        Assert.IsNull(sut.ErrorMessage);
    }

    [TestMethod]
    public async Task RefreshAsync_ServiceReturnsEmptyList_IsEmptyIsTrue()
    {
        // Arrange
        var service = new FakeVolumeManagementService { DefaultVolumes = [] };
        var sut = new VolumesViewModel(service);

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.IsEmpty(sut.Volumes);
        Assert.IsTrue(sut.IsEmpty);
    }

    [TestMethod]
    public async Task RefreshAsync_ServiceThrows_ErrorMessageIsSetAndExistingRowsArePreserved()
    {
        // Arrange
        var service = new FakeVolumeManagementService();
        service.DefaultVolumes = [CreateVolume("vol-1")];
        var sut = new VolumesViewModel(service);
        await sut.RefreshAsync();
        service.GetVolumesException = new ContainerRuntimeException("list", 1, "list failed");

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.AreEqual("list failed", sut.ErrorMessage);
        Assert.HasCount(1, sut.Volumes);
        Assert.AreEqual("vol-1", sut.Volumes[0].Name);
    }

    [TestMethod]
    public async Task CreateAsync_ServiceSucceeds_ClearsInputSetsStatusAndRefreshesVolumes()
    {
        // Arrange
        var service = new FakeVolumeManagementService
        {
            DefaultVolumes = [],
            CreateResult = CreateVolume("vol-1"),
        };
        var sut = new VolumesViewModel(service)
        {
            NewVolumeName = "  vol-1  ",
        };

        // Act
        await sut.CreateAsync();

        // Assert
        Assert.AreEqual(string.Empty, sut.NewVolumeName);
        Assert.AreEqual("Volume created.", sut.StatusMessage);
        Assert.HasCount(1, sut.Volumes);
        Assert.AreEqual("vol-1", sut.Volumes[0].Name);
        Assert.IsFalse(sut.IsCreating);
    }

    [TestMethod]
    public async Task CreateAsync_NewVolumeNameIsEmpty_ShowsValidationErrorAndDoesNotCallService()
    {
        // Arrange
        var service = new FakeVolumeManagementService();
        var sut = new VolumesViewModel(service)
        {
            NewVolumeName = string.Empty,
        };

        // Act
        await sut.CreateAsync();

        // Assert
        Assert.AreEqual("Volume name is required.", sut.ErrorMessage);
        Assert.IsFalse(sut.IsCreating);
        Assert.IsEmpty(service.CreateCalls);
    }

    [TestMethod]
    public async Task CreateAsync_ServiceThrows_ErrorMessageIsSetAndInputIsPreserved()
    {
        // Arrange
        var service = new FakeVolumeManagementService { CreateException = new ContainerRuntimeException("create", 1, "create failed") };
        var sut = new VolumesViewModel(service)
        {
            NewVolumeName = "vol-1",
        };

        // Act
        await sut.CreateAsync();

        // Assert
        Assert.AreEqual("create failed", sut.ErrorMessage);
        Assert.AreEqual("vol-1", sut.NewVolumeName);
        Assert.IsFalse(sut.IsCreating);
    }

    [TestMethod]
    public async Task CreateAsync_RefreshAfterCreateFails_ClearsSuccessStatusAndShowsRefreshError()
    {
        // Arrange
        var service = new FakeVolumeManagementService
        {
            CreateResult = CreateVolume("vol-1"),
            GetVolumesException = new ContainerRuntimeException("list", 1, "refresh failed"),
        };
        var sut = new VolumesViewModel(service)
        {
            NewVolumeName = "vol-1",
        };

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
        var service = new FakeVolumeManagementService
        {
            CreateResult = CreateVolume("vol-1"),
            CreateGate = gate,
        };
        var sut = new VolumesViewModel(service)
        {
            NewVolumeName = "vol-1",
        };

        // Act
        var createTask = sut.CreateAsync();

        // Assert
        Assert.IsTrue(sut.IsCreating);
        gate.SetResult(true);
        await createTask;
        Assert.IsFalse(sut.IsCreating);
    }

    [TestMethod]
    public async Task DeleteAsync_ServiceSucceeds_RemovesVolumeRowAndUpdatesIsEmpty()
    {
        // Arrange
        var service = new FakeVolumeManagementService
        {
            DefaultVolumes = [CreateVolume("vol-1")],
        };
        var sut = new VolumesViewModel(service);
        await sut.RefreshAsync();
        var row = sut.Volumes[0];

        // Act
        await sut.DeleteAsync(row);

        // Assert
        Assert.IsEmpty(sut.Volumes);
        Assert.IsTrue(sut.IsEmpty);
    }

    [TestMethod]
    public async Task DeleteAsync_ServiceThrows_ErrorMessageIsSetAndRowRemains()
    {
        // Arrange
        var service = new FakeVolumeManagementService
        {
            DefaultVolumes = [CreateVolume("vol-1")],
            DeleteException = new ContainerRuntimeException("delete", 1, "delete failed"),
        };
        var sut = new VolumesViewModel(service);
        await sut.RefreshAsync();
        var row = sut.Volumes[0];

        // Act
        await sut.DeleteAsync(row);

        // Assert
        Assert.AreEqual("delete failed", sut.ErrorMessage);
        Assert.HasCount(1, sut.Volumes);
        Assert.AreSame(row, sut.Volumes[0]);
    }

    [TestMethod]
    public async Task DeleteAsync_InUseRow_DoesNotCallServiceAndShowsError()
    {
        // Arrange
        var service = new FakeVolumeManagementService
        {
            DefaultVolumes = [CreateVolume("vol-1", "web")],
        };
        var sut = new VolumesViewModel(service);
        await sut.RefreshAsync();
        var row = sut.Volumes[0];

        // Act
        await sut.DeleteAsync(row);

        // Assert
        Assert.AreEqual("Volume 'vol-1' is in use by: web", sut.ErrorMessage);
        Assert.IsEmpty(service.DeleteCalls);
        Assert.HasCount(1, sut.Volumes);
    }

    [TestMethod]
    public async Task DeleteAsync_VolumeInUseException_UsesExceptionNamesInErrorMessage()
    {
        // Arrange
        var service = new FakeVolumeManagementService
        {
            DefaultVolumes = [CreateVolume("vol-1")],
            DeleteException = new VolumeInUseException("vol-1", ["web", "db"]),
        };
        var sut = new VolumesViewModel(service);
        await sut.RefreshAsync();
        var row = sut.Volumes[0];

        // Act
        await sut.DeleteAsync(row);

        // Assert
        Assert.AreEqual("Volume 'vol-1' is in use by: web, db", sut.ErrorMessage);
        Assert.HasCount(1, sut.Volumes);
    }

    [TestMethod]
    public async Task RefreshAsync_RefetchesIdenticalVolumes_PreservesRowInstances()
    {
        // Arrange
        var service = new FakeVolumeManagementService { DefaultVolumes = [CreateVolume("vol-1"), CreateVolume("vol-2")] };
        var sut = new VolumesViewModel(service);
        await sut.RefreshAsync();
        var row1 = sut.Volumes[0];
        var row2 = sut.Volumes[1];

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.HasCount(2, sut.Volumes);
        Assert.AreSame(row1, sut.Volumes[0]);
        Assert.AreSame(row2, sut.Volumes[1]);
    }

    [TestMethod]
    public async Task RefreshAsync_RefetchesIdenticalVolumes_RaisesNoCollectionChanged()
    {
        // Arrange
        var service = new FakeVolumeManagementService { DefaultVolumes = [CreateVolume("vol-1"), CreateVolume("vol-2")] };
        var sut = new VolumesViewModel(service);
        await sut.RefreshAsync();
        var actions = new List<NotifyCollectionChangedAction>();
        sut.Volumes.CollectionChanged += (_, e) => actions.Add(e.Action);

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.IsEmpty(actions);
    }

    [TestMethod]
    public async Task RefreshAsync_VolumeRemovedOnServer_RowRemovedAndOthersPreserved()
    {
        // Arrange
        var service = new FakeVolumeManagementService { DefaultVolumes = [CreateVolume("vol-1"), CreateVolume("vol-2")] };
        var sut = new VolumesViewModel(service);
        await sut.RefreshAsync();
        var row1 = sut.Volumes[0];
        service.DefaultVolumes = [CreateVolume("vol-1")];

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.HasCount(1, sut.Volumes);
        Assert.AreSame(row1, sut.Volumes[0]);
    }

    [TestMethod]
    public async Task RefreshAsync_NewVolumeOnServer_RowAddedAndExistingPreserved()
    {
        // Arrange
        var service = new FakeVolumeManagementService { DefaultVolumes = [CreateVolume("vol-1")] };
        var sut = new VolumesViewModel(service);
        await sut.RefreshAsync();
        var row1 = sut.Volumes[0];
        service.DefaultVolumes = [CreateVolume("vol-1"), CreateVolume("vol-2")];

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.HasCount(2, sut.Volumes);
        Assert.AreSame(row1, sut.Volumes[0]);
        Assert.AreEqual("vol-2", sut.Volumes[1].Name);
    }

    [TestMethod]
    public async Task RefreshAsync_VolumeReferencesChanged_ChangedRowReplacedWithNewUsageTextAndOthersPreserved()
    {
        // Arrange
        var service = new FakeVolumeManagementService { DefaultVolumes = [CreateVolume("vol-1"), CreateVolume("vol-2", "web")] };
        var sut = new VolumesViewModel(service);
        await sut.RefreshAsync();
        var row1 = sut.Volumes[0];
        var row2 = sut.Volumes[1];
        service.DefaultVolumes = [CreateVolume("vol-1"), CreateVolume("vol-2", "web", "db")];

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.HasCount(2, sut.Volumes);
        Assert.AreSame(row1, sut.Volumes[0]);
        Assert.AreNotSame(row2, sut.Volumes[1]);
        Assert.AreEqual("web, db", sut.Volumes[1].UsageText);
    }

    [TestMethod]
    public async Task RefreshAsync_RefetchesEquivalentVolumesFromFreshRecordInstances_PreservesRowInstances()
    {
        // Arrange
        var service = new FakeVolumeManagementService { DefaultVolumes = [CreateVolume("vol-1", "web")] };
        var sut = new VolumesViewModel(service);
        await sut.RefreshAsync();
        var row1 = sut.Volumes[0];
        service.DefaultVolumes = [CreateVolume("vol-1", "web")];

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.AreSame(row1, sut.Volumes[0]);
    }
}
