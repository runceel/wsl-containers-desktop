using System.Collections.Specialized;
using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Domain;
using WslContainersDesktop_App.ViewModels;
using WslContainersDesktop_App_Tests.Fakes;

namespace WslContainersDesktop_App_Tests.ViewModels;

[TestClass]
public sealed class ImagesViewModelTests
{
    private static DateTimeOffset CreatedAt => new(2026, 7, 2, 9, 0, 0, TimeSpan.Zero);

    private static ContainerImage CreateImage(string id) => new(
        Id: id,
        Repository: "ubuntu",
        Tag: "latest",
        SizeBytes: 120L,
        CreatedAt: CreatedAt);

    [TestMethod]
    public async Task RefreshAsync_ServiceReturnsImages_PopulatesRowsAndClearsErrorAndIsEmptyIsFalse()
    {
        // Arrange
        var service = new FakeImageManagementService
        {
            DefaultImages = [CreateImage("img-1"), CreateImage("img-2")],
        };
        var sut = new ImagesViewModel(service);

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.HasCount(2, sut.Images);
        Assert.AreEqual("img-1", sut.Images[0].Id);
        Assert.AreEqual("ubuntu:latest", sut.Images[0].DisplayName);
        Assert.IsFalse(sut.IsEmpty);
        Assert.IsNull(sut.ErrorMessage);
    }

    [TestMethod]
    public async Task RefreshAsync_ServiceReturnsEmptyList_IsEmptyIsTrue()
    {
        // Arrange
        var service = new FakeImageManagementService { DefaultImages = [] };
        var sut = new ImagesViewModel(service);

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.IsEmpty(sut.Images);
        Assert.IsTrue(sut.IsEmpty);
    }

    [TestMethod]
    public async Task RefreshAsync_ServiceThrows_ErrorMessageIsSetAndExistingImagesArePreserved()
    {
        // Arrange
        var service = new FakeImageManagementService();
        service.GetImagesResults.Enqueue(() => Task.FromResult<IReadOnlyList<ContainerImage>>([CreateImage("img-1")]));
        service.GetImagesResults.Enqueue(() => Task.FromException<IReadOnlyList<ContainerImage>>(new ContainerRuntimeException("list", 1, "一覧の取得に失敗しました。")));
        var sut = new ImagesViewModel(service);
        await sut.RefreshAsync();

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.AreEqual("一覧の取得に失敗しました。", sut.ErrorMessage);
        Assert.HasCount(1, sut.Images);
        Assert.AreEqual("img-1", sut.Images[0].Id);
    }

    [TestMethod]
    public async Task PullAsync_ServiceSucceeds_ClearsInputSetsStatusAndRefreshesImages()
    {
        // Arrange
        var service = new FakeImageManagementService
        {
            DefaultImages = [],
            PullResultImages = [CreateImage("img-1")],
        };
        var sut = new ImagesViewModel(service)
        {
            PullReference = "  ubuntu:latest  ",
        };

        // Act
        await sut.PullAsync();

        // Assert
        Assert.AreEqual(string.Empty, sut.PullReference);
        Assert.AreEqual("Pull completed.", sut.StatusMessage);
        Assert.HasCount(1, sut.Images);
        Assert.AreEqual("img-1", sut.Images[0].Id);
        Assert.IsFalse(sut.IsErrorMessageVisible);
        Assert.IsTrue(sut.IsStatusMessageVisible);
    }

    [TestMethod]
    public async Task PullAsync_PullReferenceIsEmpty_ShowsValidationErrorAndDoesNotCallService()
    {
        // Arrange
        var service = new FakeImageManagementService();
        var sut = new ImagesViewModel(service)
        {
            PullReference = string.Empty,
        };

        // Act
        await sut.PullAsync();

        // Assert
        Assert.AreEqual("Image reference is required.", sut.ErrorMessage);
        Assert.IsTrue(sut.IsErrorMessageVisible);
        Assert.IsNull(sut.StatusMessage);
        Assert.IsFalse(sut.IsStatusMessageVisible);
        Assert.IsFalse(sut.IsPulling);
        Assert.IsEmpty(service.PullCalls);
    }

    [TestMethod]
    public async Task PullAsync_PullReferenceIsWhiteSpace_ShowsValidationErrorAndDoesNotCallService()
    {
        // Arrange
        var service = new FakeImageManagementService();
        var sut = new ImagesViewModel(service)
        {
            PullReference = "   ",
        };

        // Act
        await sut.PullAsync();

        // Assert
        Assert.AreEqual("Image reference is required.", sut.ErrorMessage);
        Assert.IsTrue(sut.IsErrorMessageVisible);
        Assert.IsFalse(sut.IsStatusMessageVisible);
        Assert.IsEmpty(service.PullCalls);
    }

    [TestMethod]
    public async Task PullAsync_OperationIsRunning_IsPullingIsTrueThenFalse()
    {
        // Arrange
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakeImageManagementService
        {
            PullGate = gate,
            PullResultImages = [CreateImage("img-1")],
        };
        var sut = new ImagesViewModel(service)
        {
            PullReference = "ubuntu:latest",
        };

        // Act
        var pullTask = sut.PullAsync();

        // Assert
        Assert.IsTrue(sut.IsPulling);
        gate.SetResult(true);
        await pullTask;
        Assert.IsFalse(sut.IsPulling);
    }

    [TestMethod]
    public async Task PullAsync_ServiceThrows_ErrorMessageIsSetAndInputIsPreserved()
    {
        // Arrange
        var service = new FakeImageManagementService { PullException = new ContainerRuntimeException("pull", 1, "pull failed") };
        var sut = new ImagesViewModel(service)
        {
            PullReference = "ubuntu:latest",
        };

        // Act
        await sut.PullAsync();

        // Assert
        Assert.AreEqual("pull failed", sut.ErrorMessage);
        Assert.AreEqual("ubuntu:latest", sut.PullReference);
        Assert.IsTrue(sut.IsErrorMessageVisible);
        Assert.IsFalse(sut.IsStatusMessageVisible);
    }

    [TestMethod]
    public async Task DeleteAsync_AfterPullSucceeded_ClearsStaleStatusMessage()
    {
        // Arrange
        var service = new FakeImageManagementService
        {
            DefaultImages = [CreateImage("img-1")],
            PullResultImages = [CreateImage("img-1")],
        };
        var sut = new ImagesViewModel(service)
        {
            PullReference = "ubuntu:latest",
        };
        await sut.RefreshAsync();
        await sut.PullAsync();
        var row = sut.Images[0];

        // Act
        await sut.DeleteAsync(row);

        // Assert
        Assert.IsNull(sut.StatusMessage);
    }

    [TestMethod]
    public async Task DeleteAsync_ServiceSucceeds_RemovesImageRowAndUpdatesIsEmpty()
    {
        // Arrange
        var service = new FakeImageManagementService
        {
            DefaultImages = [CreateImage("img-1")],
            DeleteResultImages = [],
        };
        var sut = new ImagesViewModel(service);
        await sut.RefreshAsync();
        var row = sut.Images[0];

        // Act
        await sut.DeleteAsync(row);

        // Assert
        Assert.IsEmpty(sut.Images);
        Assert.IsTrue(sut.IsEmpty);
    }

    [TestMethod]
    public async Task DeleteAsync_ServiceThrows_ErrorMessageIsSetAndRowRemains()
    {
        // Arrange
        var service = new FakeImageManagementService
        {
            DefaultImages = [CreateImage("img-1")],
            DeleteException = new ContainerRuntimeException("delete", 1, "delete failed"),
        };
        var sut = new ImagesViewModel(service);
        await sut.RefreshAsync();
        var row = sut.Images[0];

        // Act
        await sut.DeleteAsync(row);

        // Assert
        Assert.AreEqual("delete failed", sut.ErrorMessage);
        Assert.HasCount(1, sut.Images);
        Assert.AreSame(row, sut.Images[0]);
    }

    [TestMethod]
    public async Task RefreshAsync_RefetchesIdenticalImages_PreservesRowInstances()
    {
        // Arrange
        var service = new FakeImageManagementService { DefaultImages = [CreateImage("img-1"), CreateImage("img-2")] };
        var sut = new ImagesViewModel(service);
        await sut.RefreshAsync();
        var row1 = sut.Images[0];
        var row2 = sut.Images[1];

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.HasCount(2, sut.Images);
        Assert.AreSame(row1, sut.Images[0]);
        Assert.AreSame(row2, sut.Images[1]);
    }

    [TestMethod]
    public async Task RefreshAsync_RefetchesIdenticalImages_RaisesNoCollectionChanged()
    {
        // Arrange
        var service = new FakeImageManagementService { DefaultImages = [CreateImage("img-1"), CreateImage("img-2")] };
        var sut = new ImagesViewModel(service);
        await sut.RefreshAsync();
        var actions = new List<NotifyCollectionChangedAction>();
        sut.Images.CollectionChanged += (_, e) => actions.Add(e.Action);

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.IsEmpty(actions);
    }

    [TestMethod]
    public async Task RefreshAsync_ImageRemovedOnServer_RowRemovedAndOthersPreserved()
    {
        // Arrange
        var service = new FakeImageManagementService { DefaultImages = [CreateImage("img-1"), CreateImage("img-2")] };
        var sut = new ImagesViewModel(service);
        await sut.RefreshAsync();
        var row1 = sut.Images[0];
        service.DefaultImages = [CreateImage("img-1")];

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.HasCount(1, sut.Images);
        Assert.AreSame(row1, sut.Images[0]);
    }

    [TestMethod]
    public async Task RefreshAsync_NewImageOnServer_RowAddedAndExistingPreserved()
    {
        // Arrange
        var service = new FakeImageManagementService { DefaultImages = [CreateImage("img-1")] };
        var sut = new ImagesViewModel(service);
        await sut.RefreshAsync();
        var row1 = sut.Images[0];
        service.DefaultImages = [CreateImage("img-1"), CreateImage("img-2")];

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.HasCount(2, sut.Images);
        Assert.AreSame(row1, sut.Images[0]);
        Assert.AreEqual("img-2", sut.Images[1].Id);
    }

    [TestMethod]
    public async Task RefreshAsync_ImageTagChangedForSameId_RowReplacedWithNewDisplayName()
    {
        // Arrange
        var service = new FakeImageManagementService
        {
            DefaultImages = [new ContainerImage("img-1", "ubuntu", "20.04", 120L, CreatedAt)],
        };
        var sut = new ImagesViewModel(service);
        await sut.RefreshAsync();
        var originalRow = sut.Images[0];
        service.DefaultImages = [new ContainerImage("img-1", "ubuntu", "22.04", 120L, CreatedAt)];

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.HasCount(1, sut.Images);
        Assert.AreNotSame(originalRow, sut.Images[0]);
        Assert.AreEqual("ubuntu:22.04", sut.Images[0].DisplayName);
    }

    [TestMethod]
    public async Task RefreshAsync_RefetchesEquivalentImagesFromFreshRecordInstances_PreservesRowInstances()
    {
        // Arrange
        var service = new FakeImageManagementService { DefaultImages = [CreateImage("img-1")] };
        var sut = new ImagesViewModel(service);
        await sut.RefreshAsync();
        var row1 = sut.Images[0];
        service.DefaultImages = [CreateImage("img-1")];

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.AreSame(row1, sut.Images[0]);
    }
}
