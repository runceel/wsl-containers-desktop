using WslContainersDesktop.Domain;
using WslContainersDesktop_App.ViewModels;

namespace WslContainersDesktop_App_Tests.ViewModels;

[TestClass]
public sealed class ContainerRowViewModelTests
{
    private static Container CreateContainer(ContainerState state) => new(
        Id: "c1",
        Name: "web",
        Image: "nginx:latest",
        State: state,
        CreatedAt: new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero));

    [TestMethod]
    public void Constructor_FromContainer_PropertiesAreMappedFromContainer()
    {
        // Arrange
        var container = CreateContainer(ContainerState.Running);

        // Act
        var sut = new ContainerRowViewModel(container);

        // Assert
        Assert.AreEqual("c1", sut.Id);
        Assert.AreEqual("web", sut.Name);
        Assert.AreEqual("nginx:latest", sut.Image);
        Assert.AreEqual(ContainerState.Running, sut.State);
        Assert.AreEqual(container.CreatedAt, sut.CreatedAt);
        Assert.IsTrue(sut.CanStop);
        Assert.IsFalse(sut.CanStart);
    }

    [TestMethod]
    public void ApplyFrom_ContainerWithDifferentState_UpdatesStateAndCanFlags()
    {
        // Arrange
        var sut = new ContainerRowViewModel(CreateContainer(ContainerState.Stopped));
        var updated = CreateContainer(ContainerState.Running);

        // Act
        sut.ApplyFrom(updated);

        // Assert
        Assert.AreEqual(ContainerState.Running, sut.State);
        Assert.IsTrue(sut.CanStop);
        Assert.IsTrue(sut.CanRestart);
        Assert.IsFalse(sut.CanStart);
        Assert.IsFalse(sut.CanDelete);
    }

    [TestMethod]
    public void ApplyFrom_ContainerWithDifferentState_RaisesPropertyChangedForCanFlags()
    {
        // Arrange
        var sut = new ContainerRowViewModel(CreateContainer(ContainerState.Stopped));
        var raisedProperties = new List<string>();
        sut.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName!);

        // Act
        sut.ApplyFrom(CreateContainer(ContainerState.Running));

        // Assert
        CollectionAssert.Contains(raisedProperties, nameof(ContainerRowViewModel.CanStart));
        CollectionAssert.Contains(raisedProperties, nameof(ContainerRowViewModel.CanStop));
        CollectionAssert.Contains(raisedProperties, nameof(ContainerRowViewModel.CanRestart));
        CollectionAssert.Contains(raisedProperties, nameof(ContainerRowViewModel.CanDelete));
    }

    [TestMethod]
    public void IsBusy_WhenTrue_DisablesAllCanFlags()
    {
        // Arrange
        var stoppedRow = new ContainerRowViewModel(CreateContainer(ContainerState.Stopped));
        var runningRow = new ContainerRowViewModel(CreateContainer(ContainerState.Running));

        // Act
        stoppedRow.IsBusy = true;
        runningRow.IsBusy = true;

        // Assert
        Assert.IsFalse(stoppedRow.CanStart);
        Assert.IsFalse(stoppedRow.CanDelete);
        Assert.IsFalse(runningRow.CanStop);
        Assert.IsFalse(runningRow.CanRestart);
    }

    [TestMethod]
    public void PendingOperation_DefaultValue_IsNoneAndDisplayStateReflectsState()
    {
        // Arrange & Act
        var sut = new ContainerRowViewModel(CreateContainer(ContainerState.Running));

        // Assert
        Assert.AreEqual(ContainerRowOperation.None, sut.PendingOperation);
        Assert.AreEqual(new ContainerRowDisplayState(ContainerState.Running, ContainerRowOperation.None), sut.DisplayState);
    }

    [TestMethod]
    public void PendingOperation_SetToStopping_DisplayStateReflectsPendingOperation()
    {
        // Arrange
        var sut = new ContainerRowViewModel(CreateContainer(ContainerState.Running));

        // Act
        sut.PendingOperation = ContainerRowOperation.Stopping;

        // Assert
        Assert.AreEqual(new ContainerRowDisplayState(ContainerState.Running, ContainerRowOperation.Stopping), sut.DisplayState);
    }

    [TestMethod]
    public void PendingOperation_Changed_RaisesPropertyChangedForDisplayState()
    {
        // Arrange
        var sut = new ContainerRowViewModel(CreateContainer(ContainerState.Running));
        var raisedProperties = new List<string>();
        sut.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName!);

        // Act
        sut.PendingOperation = ContainerRowOperation.Stopping;

        // Assert
        CollectionAssert.Contains(raisedProperties, nameof(ContainerRowViewModel.DisplayState));
    }

    [TestMethod]
    public void ApplyFrom_ContainerWithDifferentState_RaisesPropertyChangedForDisplayState()
    {
        // Arrange
        var sut = new ContainerRowViewModel(CreateContainer(ContainerState.Stopped));
        var raisedProperties = new List<string>();
        sut.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName!);

        // Act
        sut.ApplyFrom(CreateContainer(ContainerState.Running));

        // Assert
        CollectionAssert.Contains(raisedProperties, nameof(ContainerRowViewModel.DisplayState));
    }

    [TestMethod]
    public void ApplyFrom_WhilePendingOperationIsSet_DisplayStateCombinesNewStateWithExistingPendingOperation()
    {
        // Arrange
        var sut = new ContainerRowViewModel(CreateContainer(ContainerState.Running));
        sut.PendingOperation = ContainerRowOperation.Stopping;

        // Act
        sut.ApplyFrom(CreateContainer(ContainerState.Stopped));

        // Assert
        Assert.AreEqual(new ContainerRowDisplayState(ContainerState.Stopped, ContainerRowOperation.Stopping), sut.DisplayState);
    }
}
