namespace WslContainersDesktop_App_Tests.Pages;

[TestClass]
public sealed class VolumesPageSourceTests
{
    [TestMethod]
    public void VolumesPage_XamlDisplaysRequiredVolumeFields()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\VolumesPage.xaml");

        // Assert
        StringAssert.Contains(sourceText, "Text=\"{x:Bind Name}\"");
        StringAssert.Contains(sourceText, "Text=\"{x:Bind Driver}\"");
        StringAssert.Contains(sourceText, "Text=\"{x:Bind CreatedAt}\"");
        StringAssert.Contains(sourceText, "Text=\"{x:Bind UsageText}\"");
    }

    [TestMethod]
    public void VolumesPage_HeaderIncludesAccentBar()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\VolumesPage.xaml");

        // Assert
        StringAssert.Contains(sourceText, "<ColumnDefinition Width=\"4\" />");
        StringAssert.Contains(sourceText, "Background=\"{ThemeResource WslContainersAccentFillColorDefaultBrush}\"");
        StringAssert.Contains(sourceText, "CornerRadius=\"{StaticResource ControlCornerRadius}\"");
    }

    [TestMethod]
    public void VolumesPage_DeleteClickShowsConfirmationBeforeExecutingDeleteCommand()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\VolumesPage.xaml.cs");

        // Act
        var handlerIndex = sourceText.IndexOf("BtnDeleteVolume_Click", StringComparison.Ordinal);
        var dialogIndex = sourceText.IndexOf("ContentDialog", StringComparison.Ordinal);
        var primaryResultIndex = sourceText.IndexOf("ContentDialogResult.Primary", StringComparison.Ordinal);
        var deleteCommandIndex = sourceText.IndexOf("DeleteCommand.ExecuteAsync", StringComparison.Ordinal);

        // Assert
        Assert.IsGreaterThanOrEqualTo(0, handlerIndex, "Expected VolumesPage to define BtnDeleteVolume_Click.");
        Assert.IsGreaterThanOrEqualTo(0, dialogIndex, "Expected volume deletion to create a confirmation dialog.");
        Assert.IsGreaterThanOrEqualTo(0, primaryResultIndex, "Expected volume deletion to require primary confirmation.");
        Assert.IsGreaterThanOrEqualTo(0, deleteCommandIndex, "Expected confirmed volume deletion to execute DeleteCommand.");
        Assert.IsTrue(
            handlerIndex < dialogIndex && dialogIndex < deleteCommandIndex,
            "Expected the delete confirmation dialog to be created before executing the delete command.");
    }

    [TestMethod]
    public void MainWindow_SourceContainsVolumesNavigationItemAndMapping()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\MainWindow.xaml");
        var codeBehindText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\MainWindow.xaml.cs");

        // Assert
        StringAssert.Contains(sourceText, "AutomationProperties.AutomationId=\"NavItemVolumes\"");
        StringAssert.Contains(codeBehindText, "[NavItemVolumes] = NavigationPageKey.Volumes");
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
