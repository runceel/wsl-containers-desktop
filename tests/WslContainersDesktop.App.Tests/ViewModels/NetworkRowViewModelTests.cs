using WslContainersDesktop.Domain;
using WslContainersDesktop_App.ViewModels;

namespace WslContainersDesktop_App_Tests.ViewModels;

[TestClass]
public sealed class NetworkRowViewModelTests
{
    [TestMethod]
    public void UsageText_UserNetworkWithoutConnectionsAndMinValueCreatedAt_ShowsUnusedAndDefaultValues()
    {
        // Arrange
        var network = new ContainerNetworkResource("app-net", "bridge", DateTimeOffset.MinValue, [], false);

        // Act
        var sut = new NetworkRowViewModel(network);

        // Assert
        Assert.AreEqual("Unused", sut.UsageText);
        Assert.AreEqual("0", sut.ConnectedContainerCountText);
        Assert.AreEqual("User-created", sut.TypeText);
        Assert.AreEqual("Unknown", sut.CreatedAtText);
        Assert.IsTrue(sut.CanDelete);
    }

    [TestMethod]
    public void CreatedAtText_UserNetworkWithRealCreatedAt_IsNotUnknown()
    {
        // Arrange
        var network = new ContainerNetworkResource("app-net", "bridge", new DateTimeOffset(2026, 7, 4, 9, 0, 0, TimeSpan.Zero), [], false);

        // Act
        var sut = new NetworkRowViewModel(network);

        // Assert
        Assert.AreNotEqual("Unknown", sut.CreatedAtText);
    }

    [TestMethod]
    public void TypeText_SystemNetwork_IsSystem()
    {
        // Arrange
        var network = new ContainerNetworkResource("bridge", "bridge", new DateTimeOffset(2026, 7, 4, 9, 0, 0, TimeSpan.Zero), [], true);

        // Act
        var sut = new NetworkRowViewModel(network);

        // Assert
        Assert.AreEqual("System", sut.TypeText);
        Assert.IsFalse(sut.CanDelete);
    }
}
