using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Domain.Tests;

[TestClass]
public sealed class ContainerResourceUsageTests
{
    [TestMethod]
    public void MemoryPercentage_LimitPositive_ReturnsUsageRatioTimes100()
    {
        // Arrange
        var sut = new ContainerResourceUsage("c1", "name", 0, 536870912L, 2147483648L);

        // Act
        var actual = sut.MemoryPercentage;

        // Assert
        Assert.AreEqual(25.0, actual, 1e-9);
    }

    [TestMethod]
    public void MemoryPercentage_LimitZero_ReturnsZero()
    {
        // Arrange
        var sut = new ContainerResourceUsage("c1", "name", 0, 1000, 0);

        // Act
        var actual = sut.MemoryPercentage;

        // Assert
        Assert.AreEqual(0, actual);
    }

    [TestMethod]
    public void MemoryPercentage_LimitNegative_ReturnsZero()
    {
        // Arrange
        var sut = new ContainerResourceUsage("c1", "name", 0, 1000, -1);

        // Act
        var actual = sut.MemoryPercentage;

        // Assert
        Assert.AreEqual(0, actual);
    }
}
