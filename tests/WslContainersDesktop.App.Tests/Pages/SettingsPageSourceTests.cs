using System.Text.RegularExpressions;

namespace WslContainersDesktop_App_Tests.Pages;

[TestClass]
public sealed class SettingsPageSourceTests
{
    private const string XamlRelativePath = @"src\WslContainersDesktop.App\Pages\SettingsPage.xaml";
    private const string CodeBehindRelativePath = @"src\WslContainersDesktop.App\Pages\SettingsPage.xaml.cs";

    [TestMethod]
    public void SettingsPage_DisplaysWslIntegrationStatus()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(XamlRelativePath);

        // Assert
        StringAssert.Contains(sourceText, "Text=\"{x:Bind ViewModel.WslVersionText, Mode=OneWay}\"");
        StringAssert.Contains(sourceText, "{x:Bind ViewModel.IsWslContainersAvailable, Mode=OneWay}");
    }

    [TestMethod]
    public void SettingsPage_ShowsRequirementsWarningInfoBar()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(XamlRelativePath);
        var warningPattern = new Regex(
            """
            <InfoBar\b[^>]*Severity="Warning"[^>]*IsOpen="\{x:Bind ViewModel\.IsRequirementsWarningVisible, Mode=OneWay\}"
            """,
            RegexOptions.Singleline);

        // Assert
        Assert.IsTrue(
            warningPattern.IsMatch(sourceText),
            "Expected a Warning InfoBar bound to IsRequirementsWarningVisible.");
    }

    [TestMethod]
    public void SettingsPage_ResourceInputsUseTwoWayBindingAndAreGatedByCanEdit()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(XamlRelativePath);

        // Assert
        StringAssert.Contains(sourceText, "Text=\"{x:Bind ViewModel.MemoryMegabytesInput, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"");
        StringAssert.Contains(sourceText, "Text=\"{x:Bind ViewModel.ProcessorCountInput, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"");
        StringAssert.Contains(sourceText, "IsEnabled=\"{x:Bind ViewModel.CanEditResourceLimits, Mode=OneWay}\"");
        StringAssert.Contains(sourceText, "AutomationProperties.AutomationId=\"TxtMemoryMegabytes\"");
        StringAssert.Contains(sourceText, "AutomationProperties.AutomationId=\"TxtProcessorCount\"");
    }

    [TestMethod]
    public void SettingsPage_SaveButtonBindsToSaveCommand()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(XamlRelativePath);

        // Assert
        StringAssert.Contains(sourceText, "AutomationProperties.AutomationId=\"BtnSaveResourceLimits\"");
        StringAssert.Contains(sourceText, "Command=\"{x:Bind ViewModel.SaveCommand}\"");
    }

    [TestMethod]
    public void SettingsPage_ResetButtonUsesClickHandlerForConfirmation()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(XamlRelativePath);

        // Assert
        StringAssert.Contains(sourceText, "AutomationProperties.AutomationId=\"BtnResetResourceLimits\"");
        StringAssert.Contains(sourceText, "Click=\"BtnResetResourceLimits_Click\"");
    }

    [TestMethod]
    public void SettingsPage_ShowsErrorAndStatusInfoBars()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(XamlRelativePath);
        var errorPattern = new Regex(
            """
            <InfoBar\b[^>]*Severity="Error"[^>]*IsOpen="\{x:Bind ViewModel\.IsErrorMessageVisible, Mode=OneWay\}"
            """,
            RegexOptions.Singleline);
        var statusPattern = new Regex(
            """
            <InfoBar\b[^>]*Severity="Success"[^>]*IsOpen="\{x:Bind ViewModel\.IsStatusMessageVisible, Mode=OneWay\}"
            """,
            RegexOptions.Singleline);

        // Assert
        Assert.IsTrue(errorPattern.IsMatch(sourceText), "Expected an Error InfoBar bound to IsErrorMessageVisible.");
        Assert.IsTrue(statusPattern.IsMatch(sourceText), "Expected a Success InfoBar bound to IsStatusMessageVisible.");
        StringAssert.Contains(sourceText, "Message=\"{x:Bind ViewModel.ErrorMessage, Mode=OneWay}\"");
        StringAssert.Contains(sourceText, "Message=\"{x:Bind ViewModel.StatusMessage, Mode=OneWay}\"");
    }

    [TestMethod]
    public void SettingsPage_HeaderIncludesAccentBar()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(XamlRelativePath);

        // Assert
        StringAssert.Contains(sourceText, "<ColumnDefinition Width=\"4\" />");
        StringAssert.Contains(sourceText, "Background=\"{ThemeResource WslContainersAccentFillColorDefaultBrush}\"");
        StringAssert.Contains(sourceText, "CornerRadius=\"{StaticResource ControlCornerRadius}\"");
    }

    [TestMethod]
    public void SettingsPage_ResolvesViewModelFromDependencyInjection()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(CodeBehindRelativePath);

        // Assert
        StringAssert.Contains(sourceText, "GetRequiredService<SettingsViewModel>()");
        StringAssert.Contains(sourceText, "public SettingsViewModel ViewModel { get; }");
    }

    [TestMethod]
    public void SettingsPage_RefreshesOnLoaded()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(CodeBehindRelativePath);

        // Assert
        StringAssert.Contains(sourceText, "Loaded +=");
        StringAssert.Contains(sourceText, "RefreshCommand.ExecuteAsync(null)");
    }

    [TestMethod]
    public void SettingsPage_ResetClickShowsConfirmationBeforeExecutingResetCommand()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(CodeBehindRelativePath);

        // Act
        var handlerIndex = sourceText.IndexOf("BtnResetResourceLimits_Click", StringComparison.Ordinal);
        var dialogIndex = sourceText.IndexOf("ContentDialog", StringComparison.Ordinal);
        var primaryResultIndex = sourceText.IndexOf("ContentDialogResult.Primary", StringComparison.Ordinal);
        var resetCommandIndex = sourceText.IndexOf("ResetCommand.ExecuteAsync", StringComparison.Ordinal);

        // Assert
        Assert.IsGreaterThanOrEqualTo(0, handlerIndex, "Expected SettingsPage to define BtnResetResourceLimits_Click.");
        Assert.IsGreaterThanOrEqualTo(0, dialogIndex, "Expected reset to create a confirmation dialog.");
        Assert.IsGreaterThanOrEqualTo(0, primaryResultIndex, "Expected reset to require primary confirmation.");
        Assert.IsGreaterThanOrEqualTo(0, resetCommandIndex, "Expected confirmed reset to execute ResetCommand.");
        StringAssert.Contains(sourceText, "ShowAsync");
        Assert.IsTrue(
            handlerIndex < dialogIndex && dialogIndex < primaryResultIndex && primaryResultIndex < resetCommandIndex,
            "Expected the reset command to execute only inside the primary-confirmation branch of the dialog.");
    }

    private static string ReadRepositorySourceFile(string relativePath)
    {
        var repositoryRoot = FindRepositoryRoot();
        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(repositoryRoot, normalizedRelativePath);

        Assert.IsTrue(File.Exists(fullPath), $"Expected source file '{relativePath}' to exist at '{fullPath}'.");
        return File.ReadAllText(fullPath);
    }

    private static string FindRepositoryRoot()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            var appProjectFilePath = Path.Combine(currentDirectory.FullName, "src", "WslContainersDesktop.App", "WslContainersDesktop.App.csproj");
            if (File.Exists(appProjectFilePath))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        Assert.Fail($"Could not locate repository root from '{AppContext.BaseDirectory}'.");
        return string.Empty;
    }
}
