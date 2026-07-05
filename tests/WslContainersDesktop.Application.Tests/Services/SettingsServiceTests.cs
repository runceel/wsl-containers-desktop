using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Services;
using WslContainersDesktop.Application.Tests.Fakes;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Tests.Services;

[TestClass]
public sealed class SettingsServiceTests
{
    private static (SettingsService Sut, FakeWslEnvironmentProbe Probe, FakeWslResourceLimitsStore Store) CreateSut(
        WslEnvironmentInfo? info = null,
        WslResourceLimits? storeLimits = null)
    {
        var probe = new FakeWslEnvironmentProbe { Info = info ?? new WslEnvironmentInfo("2.9.3.0", true) };
        var store = new FakeWslResourceLimitsStore { Limits = storeLimits ?? WslResourceLimits.Defaults };
        return (new SettingsService(probe, store), probe, store);
    }

    [TestMethod]
    public async Task GetIntegrationStatusAsync_AvailableAndVersionAboveMinimum_MeetsRequirementsIsTrue()
    {
        // Arrange
        var (sut, _, _) = CreateSut(new WslEnvironmentInfo("2.9.3.0", true));

        // Act
        var status = await sut.GetIntegrationStatusAsync();

        // Assert
        Assert.IsTrue(status.MeetsRequirements);
        Assert.IsTrue(status.CanConfigureResources);
    }

    [TestMethod]
    public async Task GetIntegrationStatusAsync_VersionExactlyMinimum_MeetsRequirementsIsTrue()
    {
        // Arrange
        var (sut, _, _) = CreateSut(new WslEnvironmentInfo("2.9.3", true));

        // Act
        var status = await sut.GetIntegrationStatusAsync();

        // Assert
        Assert.IsTrue(status.MeetsRequirements);
    }

    [TestMethod]
    public async Task GetIntegrationStatusAsync_VersionBelowMinimum_MeetsRequirementsIsFalse()
    {
        // Arrange
        var (sut, _, _) = CreateSut(new WslEnvironmentInfo("2.9.2", true));

        // Act
        var status = await sut.GetIntegrationStatusAsync();

        // Assert
        Assert.IsFalse(status.MeetsRequirements);
    }

    [TestMethod]
    public async Task GetIntegrationStatusAsync_VersionWithLargerMinorComponent_MeetsRequirementsIsTrue()
    {
        // Guards against a lexicographic comparison bug where "2.10.0" < "2.9.3".
        // Arrange
        var (sut, _, _) = CreateSut(new WslEnvironmentInfo("2.10.0", true));

        // Act
        var status = await sut.GetIntegrationStatusAsync();

        // Assert
        Assert.IsTrue(status.MeetsRequirements);
    }

    [TestMethod]
    public async Task GetIntegrationStatusAsync_WslcNotAvailable_MeetsRequirementsIsFalse()
    {
        // Arrange
        var (sut, _, _) = CreateSut(new WslEnvironmentInfo("2.9.3.0", false));

        // Act
        var status = await sut.GetIntegrationStatusAsync();

        // Assert
        Assert.IsFalse(status.MeetsRequirements);
    }

    [TestMethod]
    public async Task GetIntegrationStatusAsync_VersionNull_MeetsRequirementsIsFalse()
    {
        // Arrange
        var (sut, _, _) = CreateSut(new WslEnvironmentInfo(null, true));

        // Act
        var status = await sut.GetIntegrationStatusAsync();

        // Assert
        Assert.IsFalse(status.MeetsRequirements);
        Assert.IsFalse(status.IsWslDetected);
    }

    [TestMethod]
    public async Task GetIntegrationStatusAsync_UnparseableVersion_MeetsRequirementsIsFalse()
    {
        // Arrange
        var (sut, _, _) = CreateSut(new WslEnvironmentInfo("not-a-version", true));

        // Act
        var status = await sut.GetIntegrationStatusAsync();

        // Assert
        Assert.IsFalse(status.MeetsRequirements);
    }

    [TestMethod]
    public async Task GetIntegrationStatusAsync_Always_PassesThroughVersionAndAvailability()
    {
        // Arrange
        var (sut, _, _) = CreateSut(new WslEnvironmentInfo("2.9.3.0", true));

        // Act
        var status = await sut.GetIntegrationStatusAsync();

        // Assert
        Assert.AreEqual("2.9.3.0", status.WslVersion);
        Assert.IsTrue(status.IsWslContainersAvailable);
    }

    [TestMethod]
    public async Task GetResourceLimitsAsync_DelegatesToStore()
    {
        // Arrange
        var (sut, _, _) = CreateSut(storeLimits: new WslResourceLimits(4096, 2));

        // Act
        var limits = await sut.GetResourceLimitsAsync();

        // Assert
        Assert.AreEqual(4096, limits.MemoryMegabytes);
        Assert.AreEqual(2, limits.ProcessorCount);
    }

    [TestMethod]
    public async Task SaveResourceLimitsAsync_RequirementsMetAndLimitsValid_SavesToStore()
    {
        // Arrange
        var (sut, _, store) = CreateSut(new WslEnvironmentInfo("2.9.3.0", true));

        // Act
        await sut.SaveResourceLimitsAsync(new WslResourceLimits(4096, 2));

        // Assert
        Assert.HasCount(1, store.SaveCalls);
        Assert.AreEqual(4096, store.SaveCalls[0].MemoryMegabytes);
        Assert.AreEqual(2, store.SaveCalls[0].ProcessorCount);
    }

    [TestMethod]
    public async Task SaveResourceLimitsAsync_DefaultLimits_SavesToStore()
    {
        // Arrange
        var (sut, _, store) = CreateSut(new WslEnvironmentInfo("2.9.3.0", true));

        // Act
        await sut.SaveResourceLimitsAsync(WslResourceLimits.Defaults);

        // Assert
        Assert.HasCount(1, store.SaveCalls);
        Assert.IsTrue(store.SaveCalls[0].IsDefault);
    }

    [TestMethod]
    public async Task SaveResourceLimitsAsync_RequirementsNotMet_ThrowsAndDoesNotSave()
    {
        // Arrange
        var (sut, _, store) = CreateSut(new WslEnvironmentInfo("2.9.3.0", false));

        // Act & Assert
        await Assert.ThrowsExactlyAsync<WslRequirementsNotMetException>(
            () => sut.SaveResourceLimitsAsync(new WslResourceLimits(4096, 2)));
        Assert.IsEmpty(store.SaveCalls);
    }

    [TestMethod]
    public async Task SaveResourceLimitsAsync_LimitsInvalid_ThrowsAndDoesNotSave()
    {
        // Arrange
        var (sut, _, store) = CreateSut(new WslEnvironmentInfo("2.9.3.0", true));

        // Act & Assert
        await Assert.ThrowsExactlyAsync<InvalidResourceLimitsException>(
            () => sut.SaveResourceLimitsAsync(new WslResourceLimits(0, 2)));
        Assert.IsEmpty(store.SaveCalls);
    }

    [TestMethod]
    public async Task SaveResourceLimitsAsync_RequirementsNotMetAndLimitsInvalid_ThrowsRequirementsFirst()
    {
        // Requirements must be checked before value validation (fail-fast on environment).
        // Arrange
        var (sut, _, store) = CreateSut(new WslEnvironmentInfo("2.9.2", true));

        // Act & Assert
        await Assert.ThrowsExactlyAsync<WslRequirementsNotMetException>(
            () => sut.SaveResourceLimitsAsync(new WslResourceLimits(0, 2)));
        Assert.IsEmpty(store.SaveCalls);
    }

    [TestMethod]
    public async Task ResetResourceLimitsAsync_RequirementsMet_SavesDefaultsToStore()
    {
        // Arrange
        var (sut, _, store) = CreateSut(new WslEnvironmentInfo("2.9.3.0", true), new WslResourceLimits(4096, 2));

        // Act
        await sut.ResetResourceLimitsAsync();

        // Assert
        Assert.HasCount(1, store.SaveCalls);
        Assert.IsTrue(store.SaveCalls[0].IsDefault);
    }

    [TestMethod]
    public async Task ResetResourceLimitsAsync_RequirementsNotMet_ThrowsAndDoesNotSave()
    {
        // Arrange
        var (sut, _, store) = CreateSut(new WslEnvironmentInfo("2.9.2", true));

        // Act & Assert
        await Assert.ThrowsExactlyAsync<WslRequirementsNotMetException>(
            () => sut.ResetResourceLimitsAsync());
        Assert.IsEmpty(store.SaveCalls);
    }
}
