using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Domain;
using WslContainersDesktop_App.ViewModels;
using WslContainersDesktop_App_Tests.Fakes;

namespace WslContainersDesktop_App_Tests.ViewModels;

[TestClass]
public sealed class SettingsViewModelTests
{
    private static FakeSettingsService MetService(WslResourceLimits? limits = null) => new()
    {
        Status = new WslIntegrationStatus("2.9.3", true, true),
        Limits = limits ?? WslResourceLimits.Defaults,
    };

    private static async Task<(SettingsViewModel Vm, FakeSettingsService Service)> CreateRefreshedMetAsync(
        WslResourceLimits? limits = null)
    {
        var service = MetService(limits);
        var vm = new SettingsViewModel(service);
        await vm.RefreshAsync();
        return (vm, service);
    }

    // ---- Refresh ----

    [TestMethod]
    public async Task RefreshAsync_MeetsRequirements_PopulatesStatusAndEnablesEditing()
    {
        // Arrange
        var service = MetService(new WslResourceLimits(4096, 2));
        var sut = new SettingsViewModel(service);

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.AreEqual("2.9.3", sut.WslVersionText);
        Assert.IsTrue(sut.IsWslContainersAvailable);
        Assert.IsTrue(sut.MeetsRequirements);
        Assert.IsTrue(sut.IsWslDetected);
        Assert.IsTrue(sut.CanEditResourceLimits);
        Assert.IsFalse(sut.IsRequirementsWarningVisible);
        Assert.AreEqual("4096", sut.MemoryMegabytesInput);
        Assert.AreEqual("2", sut.ProcessorCountInput);
    }

    [TestMethod]
    public async Task RefreshAsync_LimitsAreDefault_LeavesInputsEmpty()
    {
        // Arrange
        var (sut, _) = await CreateRefreshedMetAsync();

        // Assert
        Assert.AreEqual(string.Empty, sut.MemoryMegabytesInput);
        Assert.AreEqual(string.Empty, sut.ProcessorCountInput);
    }

    [TestMethod]
    public async Task RefreshAsync_WslVersionNull_WslVersionTextIsNotDetected()
    {
        // Arrange
        var service = new FakeSettingsService { Status = new WslIntegrationStatus(null, false, false) };
        var sut = new SettingsViewModel(service);

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.AreEqual(SettingsViewModel.NotDetectedText, sut.WslVersionText);
        Assert.IsFalse(sut.IsWslDetected);
    }

    [TestMethod]
    public async Task RefreshAsync_RequirementsNotMet_ShowsWarningAndDisablesEditing()
    {
        // Arrange
        var service = new FakeSettingsService { Status = new WslIntegrationStatus("2.9.2", true, false) };
        var sut = new SettingsViewModel(service);

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.IsFalse(sut.MeetsRequirements);
        Assert.IsTrue(sut.IsRequirementsWarningVisible);
        Assert.IsFalse(sut.CanEditResourceLimits);
    }

    [TestMethod]
    public async Task RefreshAsync_ServiceThrows_SetsErrorMessage()
    {
        // Arrange
        var service = new FakeSettingsService { GetStatusException = new WslSettingsAccessException("probe failed") };
        var sut = new SettingsViewModel(service);

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.AreEqual("probe failed", sut.ErrorMessage);
        Assert.IsTrue(sut.IsErrorMessageVisible);
        Assert.IsFalse(sut.IsLoading);
    }

    [TestMethod]
    public async Task RefreshAsync_GetResourceLimitsThrows_SetsErrorMessage()
    {
        // Arrange
        var service = MetService();
        service.GetLimitsException = new WslSettingsAccessException("cannot read .wslconfig");
        var sut = new SettingsViewModel(service);

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.AreEqual("cannot read .wslconfig", sut.ErrorMessage);
        Assert.IsTrue(sut.IsErrorMessageVisible);
        Assert.IsFalse(sut.IsLoading);
    }

    [TestMethod]
    public async Task RefreshAsync_GetResourceLimitsThrows_DisablesResourceEditing()
    {
        // Even though requirements are met, a failed load must not leave the resource inputs
        // editable with stale/blank values (which could overwrite .wslconfig on a later Save).
        // Arrange
        var service = MetService();
        service.GetLimitsException = new WslSettingsAccessException("cannot read .wslconfig");
        var sut = new SettingsViewModel(service);

        // Act
        await sut.RefreshAsync();

        // Assert
        Assert.IsTrue(sut.MeetsRequirements);
        Assert.IsFalse(sut.CanEditResourceLimits);
    }

    // ---- Save ----

    [TestMethod]
    public async Task SaveAsync_ValidInputs_CallsServiceAndSetsSavedStatus()
    {
        // Arrange
        var (sut, service) = await CreateRefreshedMetAsync();
        sut.MemoryMegabytesInput = "4096";
        sut.ProcessorCountInput = "2";

        // Act
        await sut.SaveAsync();

        // Assert
        Assert.HasCount(1, service.SaveCalls);
        Assert.AreEqual(4096, service.SaveCalls[0].MemoryMegabytes);
        Assert.AreEqual(2, service.SaveCalls[0].ProcessorCount);
        Assert.AreEqual(SettingsViewModel.SavedStatusMessage, sut.StatusMessage);
        Assert.IsTrue(sut.IsStatusMessageVisible);
        Assert.IsNull(sut.ErrorMessage);
        Assert.IsFalse(sut.IsSaving);
    }

    [TestMethod]
    public async Task SaveAsync_BlankInputs_SavesDefaultLimits()
    {
        // Arrange
        var (sut, service) = await CreateRefreshedMetAsync();
        sut.MemoryMegabytesInput = string.Empty;
        sut.ProcessorCountInput = "   ";

        // Act
        await sut.SaveAsync();

        // Assert
        Assert.HasCount(1, service.SaveCalls);
        Assert.IsTrue(service.SaveCalls[0].IsDefault);
        Assert.AreEqual(SettingsViewModel.SavedStatusMessage, sut.StatusMessage);
    }

    [TestMethod]
    public async Task SaveAsync_Success_ReloadsInputsFromStore()
    {
        // Arrange
        var (sut, service) = await CreateRefreshedMetAsync();
        var reloadsBeforeSave = service.GetLimitsCalls;

        // Leading zeros prove the inputs are re-read from the store (normalized), not just left as typed.
        sut.MemoryMegabytesInput = "08192";
        sut.ProcessorCountInput = "04";

        // Act
        await sut.SaveAsync();

        // Assert
        Assert.AreEqual("8192", sut.MemoryMegabytesInput);
        Assert.AreEqual("4", sut.ProcessorCountInput);
        Assert.IsGreaterThanOrEqualTo(reloadsBeforeSave + 1, service.GetLimitsCalls);
    }

    [TestMethod]
    public async Task SaveAsync_ThenReopenWithNewViewModel_ShowsPersistedValues()
    {
        // Arrange
        var (sut, service) = await CreateRefreshedMetAsync();
        sut.MemoryMegabytesInput = "4096";
        sut.ProcessorCountInput = "2";
        await sut.SaveAsync();

        // Act — simulate reopening the settings screen against the same (persisted) service state.
        var reopened = new SettingsViewModel(service);
        await reopened.RefreshAsync();

        // Assert
        Assert.AreEqual("4096", reopened.MemoryMegabytesInput);
        Assert.AreEqual("2", reopened.ProcessorCountInput);
    }

    [DataTestMethod]
    [DataRow("abc")]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("3.5")]
    public async Task SaveAsync_InvalidMemoryInput_ShowsInvalidInputAndDoesNotCallService(string memory)
    {
        // Arrange
        var (sut, service) = await CreateRefreshedMetAsync();
        sut.MemoryMegabytesInput = memory;
        sut.ProcessorCountInput = "2";

        // Act
        await sut.SaveAsync();

        // Assert
        Assert.AreEqual(SettingsViewModel.InvalidInputMessage, sut.ErrorMessage);
        Assert.IsEmpty(service.SaveCalls);
    }

    [DataTestMethod]
    [DataRow("abc")]
    [DataRow("0")]
    [DataRow("-2")]
    public async Task SaveAsync_InvalidProcessorInput_ShowsInvalidInputAndDoesNotCallService(string processors)
    {
        // Arrange
        var (sut, service) = await CreateRefreshedMetAsync();
        sut.MemoryMegabytesInput = "4096";
        sut.ProcessorCountInput = processors;

        // Act
        await sut.SaveAsync();

        // Assert
        Assert.AreEqual(SettingsViewModel.InvalidInputMessage, sut.ErrorMessage);
        Assert.IsEmpty(service.SaveCalls);
    }

    [TestMethod]
    public async Task SaveAsync_RequirementsNotMet_ShowsErrorAndDoesNotCallService()
    {
        // Arrange
        var service = new FakeSettingsService { Status = new WslIntegrationStatus("2.9.2", true, false) };
        var sut = new SettingsViewModel(service);
        await sut.RefreshAsync();
        sut.MemoryMegabytesInput = "4096";

        // Act
        await sut.SaveAsync();

        // Assert
        Assert.AreEqual(SettingsViewModel.RequirementsNotMetMessage, sut.ErrorMessage);
        Assert.IsEmpty(service.SaveCalls);
    }

    [TestMethod]
    public async Task SaveAsync_ServiceThrows_SetsErrorMessageFromException()
    {
        // Arrange
        var (sut, service) = await CreateRefreshedMetAsync();
        service.SaveException = new WslSettingsAccessException("cannot write .wslconfig");
        sut.MemoryMegabytesInput = "4096";

        // Act
        await sut.SaveAsync();

        // Assert
        Assert.AreEqual("cannot write .wslconfig", sut.ErrorMessage);
        Assert.IsNull(sut.StatusMessage);
        Assert.IsFalse(sut.IsSaving);
    }

    [TestMethod]
    public async Task SaveAsync_WhileRunning_IsSavingIsTrueThenFalse()
    {
        // Arrange
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var (sut, service) = await CreateRefreshedMetAsync();
        service.SaveGate = gate;
        sut.MemoryMegabytesInput = "4096";

        // Act
        var saveTask = sut.SaveAsync();

        // Assert
        Assert.IsTrue(sut.IsSaving);
        Assert.IsFalse(sut.CanEditResourceLimits);
        gate.SetResult(true);
        await saveTask;
        Assert.IsFalse(sut.IsSaving);
        Assert.IsTrue(sut.CanEditResourceLimits);
    }

    // ---- Reset ----

    [TestMethod]
    public async Task ResetAsync_MeetsRequirements_CallsServiceClearsInputsAndSetsResetStatus()
    {
        // Arrange
        var (sut, service) = await CreateRefreshedMetAsync(new WslResourceLimits(4096, 2));

        // Act
        await sut.ResetAsync();

        // Assert
        Assert.AreEqual(1, service.ResetCalls);
        Assert.AreEqual(string.Empty, sut.MemoryMegabytesInput);
        Assert.AreEqual(string.Empty, sut.ProcessorCountInput);
        Assert.AreEqual(SettingsViewModel.ResetStatusMessage, sut.StatusMessage);
    }

    [TestMethod]
    public async Task ResetAsync_RequirementsNotMet_ShowsErrorAndDoesNotCallService()
    {
        // Arrange
        var service = new FakeSettingsService { Status = new WslIntegrationStatus("2.9.2", true, false) };
        var sut = new SettingsViewModel(service);
        await sut.RefreshAsync();

        // Act
        await sut.ResetAsync();

        // Assert
        Assert.AreEqual(SettingsViewModel.RequirementsNotMetMessage, sut.ErrorMessage);
        Assert.AreEqual(0, service.ResetCalls);
    }

    [TestMethod]
    public async Task ResetAsync_ServiceThrows_SetsErrorMessageFromException()
    {
        // Arrange
        var (sut, service) = await CreateRefreshedMetAsync(new WslResourceLimits(4096, 2));
        service.ResetException = new WslSettingsAccessException("cannot write .wslconfig");

        // Act
        await sut.ResetAsync();

        // Assert
        Assert.AreEqual("cannot write .wslconfig", sut.ErrorMessage);
        Assert.IsNull(sut.StatusMessage);
    }

    // ---- Message constants (ADR-0011: no auto shutdown, announce restart instead) ----

    [TestMethod]
    public void SavedStatusMessage_AnnouncesRestartRequirement()
    {
        // The save flow must never auto-run 'wsl --shutdown' (ADR-0011); it announces the restart instead.
        StringAssert.Contains(SettingsViewModel.SavedStatusMessage, "wsl --shutdown");
    }

    [TestMethod]
    public void ResetStatusMessage_AnnouncesRestartRequirement()
    {
        StringAssert.Contains(SettingsViewModel.ResetStatusMessage, "wsl --shutdown");
    }
}
