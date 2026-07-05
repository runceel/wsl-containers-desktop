using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Domain.Tests;

[TestClass]
public sealed class WslResourceLimitsTests
{
    [TestMethod]
    public void Defaults_ReturnsInstanceWithNullValues()
    {
        // Act
        var sut = WslResourceLimits.Defaults;

        // Assert
        Assert.IsNull(sut.MemoryMegabytes);
        Assert.IsNull(sut.ProcessorCount);
        Assert.IsTrue(sut.IsDefault);
        Assert.IsTrue(sut.IsValid);
    }

    [TestMethod]
    public void IsDefault_BothValuesNull_ReturnsTrue()
    {
        // Arrange
        var sut = new WslResourceLimits(null, null);

        // Act & Assert
        Assert.IsTrue(sut.IsDefault);
    }

    [TestMethod]
    public void IsDefault_MemorySpecified_ReturnsFalse()
    {
        // Arrange
        var sut = new WslResourceLimits(4096, null);

        // Act & Assert
        Assert.IsFalse(sut.IsDefault);
    }

    [TestMethod]
    public void IsDefault_ProcessorsSpecified_ReturnsFalse()
    {
        // Arrange
        var sut = new WslResourceLimits(null, 4);

        // Act & Assert
        Assert.IsFalse(sut.IsDefault);
    }

    [DataTestMethod]
    [DataRow(null, null, true)]
    [DataRow(4096, 2, true)]
    [DataRow(1, 1, true)]
    [DataRow(null, 4, true)]
    [DataRow(8192, null, true)]
    [DataRow(0, 2, false)]
    [DataRow(-1, 2, false)]
    [DataRow(4096, 0, false)]
    [DataRow(4096, -2, false)]
    public void IsValid_VariousValues_ReturnsExpected(int? memoryMegabytes, int? processorCount, bool expected)
    {
        // Arrange
        var sut = new WslResourceLimits(memoryMegabytes, processorCount);

        // Act & Assert
        Assert.AreEqual(expected, sut.IsValid);
    }
}
