using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Domain.Tests;

[TestClass]
public sealed class WslIntegrationStatusTests
{
    [TestMethod]
    public void IsWslDetected_VersionPresent_ReturnsTrue()
    {
        // Arrange
        var sut = new WslIntegrationStatus("2.9.3.0", true, true);

        // Act & Assert
        Assert.IsTrue(sut.IsWslDetected);
    }

    [TestMethod]
    public void IsWslDetected_VersionNull_ReturnsFalse()
    {
        // Arrange
        var sut = new WslIntegrationStatus(null, false, false);

        // Act & Assert
        Assert.IsFalse(sut.IsWslDetected);
    }

    [TestMethod]
    public void IsWslDetected_VersionEmpty_ReturnsFalse()
    {
        // Arrange
        var sut = new WslIntegrationStatus("", false, false);

        // Act & Assert
        Assert.IsFalse(sut.IsWslDetected);
    }

    [TestMethod]
    public void CanConfigureResources_MeetsRequirementsTrue_ReturnsTrue()
    {
        // Arrange
        var sut = new WslIntegrationStatus("2.9.3.0", true, true);

        // Act & Assert
        Assert.IsTrue(sut.CanConfigureResources);
    }

    [TestMethod]
    public void CanConfigureResources_MeetsRequirementsFalse_ReturnsFalse()
    {
        // Arrange
        var sut = new WslIntegrationStatus("2.9.3.0", true, false);

        // Act & Assert
        Assert.IsFalse(sut.CanConfigureResources);
    }
}
