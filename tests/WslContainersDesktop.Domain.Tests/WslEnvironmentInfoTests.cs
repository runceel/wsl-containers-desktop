using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Domain.Tests;

[TestClass]
public sealed class WslEnvironmentInfoTests
{
    [TestMethod]
    public void IsWslDetected_VersionPresent_ReturnsTrue()
    {
        // Arrange
        var sut = new WslEnvironmentInfo("2.9.3.0", true);

        // Act & Assert
        Assert.IsTrue(sut.IsWslDetected);
    }

    [TestMethod]
    public void IsWslDetected_VersionNull_ReturnsFalse()
    {
        // Arrange
        var sut = new WslEnvironmentInfo(null, false);

        // Act & Assert
        Assert.IsFalse(sut.IsWslDetected);
    }

    [TestMethod]
    public void IsWslDetected_VersionEmpty_ReturnsFalse()
    {
        // Arrange
        var sut = new WslEnvironmentInfo("", true);

        // Act & Assert
        Assert.IsFalse(sut.IsWslDetected);
    }
}
