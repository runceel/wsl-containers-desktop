using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Domain.Tests;

[TestClass]
public sealed class ContainerVolumeTests
{
    [TestMethod]
    public void ContainerVolume_NoReferences_IsUnusedAndCanDelete()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero);
        var sut = new ContainerVolume("vol-demo", "guest", createdAt, []);

        // Act & Assert
        Assert.IsFalse(sut.IsInUse);
        Assert.IsTrue(sut.CanDelete);
    }

    [TestMethod]
    public void ContainerVolume_WithReferences_IsInUseAndCannotDelete()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero);
        var sut = new ContainerVolume("vol-demo", "guest", createdAt, ["web", "db"]);

        // Act & Assert
        Assert.IsTrue(sut.IsInUse);
        Assert.IsFalse(sut.CanDelete);
    }
}
