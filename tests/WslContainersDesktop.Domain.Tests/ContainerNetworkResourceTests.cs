using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Domain.Tests;

[TestClass]
public sealed class ContainerNetworkResourceTests
{
    private static DateTimeOffset CreatedAt => new(2026, 7, 4, 9, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Constructor_UserNetworkWithoutConnections_IsUnusedAndCanDelete()
    {
        // Arrange
        var sut = new ContainerNetworkResource("app-net", "bridge", CreatedAt, [], false);

        // Act & Assert
        Assert.IsFalse(sut.IsInUse);
        Assert.AreEqual(0, sut.ConnectedContainerCount);
        Assert.IsTrue(sut.CanDelete);
    }

    [TestMethod]
    public void Constructor_UserNetworkWithConnections_IsInUseAndCannotDelete()
    {
        // Arrange
        var sut = new ContainerNetworkResource("app-net", "bridge", CreatedAt, ["web", "db"], false);

        // Act & Assert
        Assert.AreEqual(2, sut.ConnectedContainerCount);
        Assert.IsTrue(sut.IsInUse);
        Assert.IsFalse(sut.CanDelete);
    }

    [TestMethod]
    public void Constructor_SystemNetworkWithoutConnections_IsSystemAndCannotDelete()
    {
        // Arrange
        var sut = new ContainerNetworkResource("bridge", "bridge", CreatedAt, [], true);

        // Act & Assert
        Assert.IsTrue(sut.IsSystem);
        Assert.IsFalse(sut.CanDelete);
    }
}
