using WslContainersDesktop.Domain;
using WslContainersDesktop_App.Converters;
using WslContainersDesktop_App.ViewModels;

namespace WslContainersDesktop_App_Tests.Converters;

[TestClass]
public sealed class ContainerDisplayStateResourceKeySelectorTests
{
    [TestMethod]
    public void GetResourceKey_RunningWithNoPendingOperation_ReturnsRunningKey()
    {
        // Arrange
        var displayState = new ContainerRowDisplayState(ContainerState.Running, ContainerRowOperation.None);

        // Act
        var key = ContainerDisplayStateResourceKeySelector.GetResourceKey(displayState);

        // Assert
        Assert.AreEqual("ContainerState_Running", key);
    }

    [TestMethod]
    public void GetResourceKey_StoppedWithNoPendingOperation_ReturnsStoppedKey()
    {
        // Arrange
        var displayState = new ContainerRowDisplayState(ContainerState.Stopped, ContainerRowOperation.None);

        // Act
        var key = ContainerDisplayStateResourceKeySelector.GetResourceKey(displayState);

        // Assert
        Assert.AreEqual("ContainerState_Stopped", key);
    }

    [TestMethod]
    public void GetResourceKey_PendingStarting_ReturnsStartingKeyRegardlessOfState()
    {
        // Arrange
        var displayState = new ContainerRowDisplayState(ContainerState.Stopped, ContainerRowOperation.Starting);

        // Act
        var key = ContainerDisplayStateResourceKeySelector.GetResourceKey(displayState);

        // Assert
        Assert.AreEqual("ContainerState_Starting", key);
    }

    [TestMethod]
    public void GetResourceKey_PendingStopping_ReturnsStoppingKeyRegardlessOfState()
    {
        // Arrange
        var displayState = new ContainerRowDisplayState(ContainerState.Running, ContainerRowOperation.Stopping);

        // Act
        var key = ContainerDisplayStateResourceKeySelector.GetResourceKey(displayState);

        // Assert
        Assert.AreEqual("ContainerState_Stopping", key);
    }

    [TestMethod]
    public void GetResourceKey_PendingRestarting_ReturnsRestartingKey()
    {
        // Arrange
        var displayState = new ContainerRowDisplayState(ContainerState.Running, ContainerRowOperation.Restarting);

        // Act
        var key = ContainerDisplayStateResourceKeySelector.GetResourceKey(displayState);

        // Assert
        Assert.AreEqual("ContainerState_Restarting", key);
    }

    [TestMethod]
    public void GetResourceKey_PendingDeleting_ReturnsDeletingKey()
    {
        // Arrange
        var displayState = new ContainerRowDisplayState(ContainerState.Stopped, ContainerRowOperation.Deleting);

        // Act
        var key = ContainerDisplayStateResourceKeySelector.GetResourceKey(displayState);

        // Assert
        Assert.AreEqual("ContainerState_Deleting", key);
    }
}
