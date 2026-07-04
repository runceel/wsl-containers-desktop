using WslContainersDesktop.Domain;
using WslContainersDesktop_App.ViewModels;

namespace WslContainersDesktop_App_Tests.ViewModels;

[TestClass]
public sealed class VolumeRowViewModelTests
{
    [TestMethod]
    public void UsageText_NoReferences_ReturnsUnused()
    {
        // Arrange
        var volume = new ContainerVolume("vol-demo", "guest", new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero), []);

        // Act
        var sut = new VolumeRowViewModel(volume);

        // Assert
        Assert.AreEqual("Unused", sut.UsageText);
        Assert.IsTrue(sut.CanDelete);
    }

    [TestMethod]
    public void UsageText_WithReferences_ReturnsCommaSeparatedNames()
    {
        // Arrange
        var volume = new ContainerVolume("vol-demo", "guest", new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero), ["web", "db"]);

        // Act
        var sut = new VolumeRowViewModel(volume);

        // Assert
        Assert.AreEqual("web, db", sut.UsageText);
        Assert.IsFalse(sut.CanDelete);
        Assert.IsTrue(sut.IsInUse);
    }
}
