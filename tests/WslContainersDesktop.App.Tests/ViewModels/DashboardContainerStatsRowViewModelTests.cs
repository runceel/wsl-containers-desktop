using System.Globalization;
using WslContainersDesktop.Domain;
using WslContainersDesktop_App.ViewModels;

namespace WslContainersDesktop_App_Tests.ViewModels;

[TestClass]
public sealed class DashboardContainerStatsRowViewModelTests
{
    private static DashboardContainerStatsRowViewModel CreateSut(double cpu, long usage, long limit, string id = "c1", string name = "web")
        => new(new ContainerResourceUsage(id, name, cpu, usage, limit));

    [TestMethod]
    public void Constructor_MapsIdentity()
    {
        var sut = CreateSut(12.34, 536870912, 2147483648, "c1", "web");
        Assert.AreEqual("c1", sut.ContainerId);
        Assert.AreEqual("web", sut.Name);
    }

    [TestMethod]
    public void CpuText_FormatsOneDecimalWithUnit()
    {
        var sut = CreateSut(12.34, 100, 0);
        Assert.AreEqual("12.3 %", sut.CpuText);
    }

    [TestMethod]
    public void CpuText_Zero_FormatsZero()
    {
        var sut = CreateSut(0, 100, 0);
        Assert.AreEqual("0.0 %", sut.CpuText);
    }

    [TestMethod]
    public void MemoryText_LimitPositive_ShowsUsageSlashLimit()
    {
        var sut = CreateSut(0, 536870912, 2147483648);
        Assert.AreEqual("512.0 MiB", sut.MemoryUsageText);
        Assert.AreEqual("512.0 MiB / 2.0 GiB", sut.MemoryText);
    }

    [TestMethod]
    public void MemoryText_LimitZero_ShowsUsageOnly()
    {
        var sut = CreateSut(0, 1610612736, 0);
        Assert.AreEqual("1.5 GiB", sut.MemoryText);
    }

    [TestMethod]
    public void MemoryUsageText_SubKibibyte_ShowsBytes()
    {
        var sut = CreateSut(0, 512, 0);
        Assert.AreEqual("512 B", sut.MemoryUsageText);
        Assert.AreEqual("512 B", sut.MemoryText);
    }

    [TestMethod]
    public void MemoryUsageText_Zero_ShowsZeroBytes()
    {
        var sut = CreateSut(0, 0, 0);
        Assert.AreEqual("0 B", sut.MemoryUsageText);
    }

    [TestMethod]
    public void MemoryUsageText_ExactKibibyte_ShowsOneDecimal()
    {
        var sut = CreateSut(0, 1536, 0);
        Assert.AreEqual("1.5 KiB", sut.MemoryUsageText);
    }

    [TestMethod]
    public void Text_UnderNonInvariantCulture_UsesInvariantFormatting()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            var sut = CreateSut(12.34, 1610612736, 0);
            Assert.AreEqual("12.3 %", sut.CpuText);
            Assert.AreEqual("1.5 GiB", sut.MemoryUsageText);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
