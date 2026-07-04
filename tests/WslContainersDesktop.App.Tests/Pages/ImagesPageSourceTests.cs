namespace WslContainersDesktop_App_Tests.Pages;

[TestClass]
public sealed class ImagesPageSourceTests
{
    [TestMethod]
    public void ImagesPage_XamlDisplaysRequiredImageFields()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ImagesPage.xaml");

        // Act
        // No-op: the test validates source bindings required by the feature specification.

        // Assert
        StringAssert.Contains(sourceText, "Text=\"{x:Bind DisplayName}\"");
        StringAssert.Contains(sourceText, "Text=\"{x:Bind Id}\"");
        StringAssert.Contains(sourceText, "CreatedAt");
    }

    [TestMethod]
    public void ImagesPage_HeaderIncludesAccentBar()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ImagesPage.xaml");

        // Assert
        StringAssert.Contains(sourceText, "<ColumnDefinition Width=\"4\" />");
        StringAssert.Contains(sourceText, "Background=\"{ThemeResource WslContainersAccentFillColorDefaultBrush}\"");
        StringAssert.Contains(sourceText, "CornerRadius=\"{StaticResource ControlCornerRadius}\"");
    }

    [TestMethod]
    public void ImagesPage_ListSectionIncludesColumnHeaders()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ImagesPage.xaml");

        // Assert
        StringAssert.Contains(sourceText, "x:Name=\"TxtImageColumnName\"");
        StringAssert.Contains(sourceText, "x:Name=\"TxtImageColumnId\"");
        StringAssert.Contains(sourceText, "x:Name=\"TxtImageColumnSize\"");
        StringAssert.Contains(sourceText, "x:Name=\"TxtImageColumnCreatedAt\"");
        StringAssert.Contains(sourceText, "Grid.Row=\"1\"");
        StringAssert.Contains(sourceText, "Background=\"{ThemeResource CardBackgroundFillColorDefaultBrush}\"");
    }

    [TestMethod]
    public void ImagesPage_DeleteClickShowsConfirmationBeforeExecutingDeleteCommand()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ImagesPage.xaml.cs");

        // Act
        var handlerIndex = sourceText.IndexOf("BtnDeleteImage_Click", StringComparison.Ordinal);
        var dialogIndex = sourceText.IndexOf("ContentDialog", StringComparison.Ordinal);
        var primaryResultIndex = sourceText.IndexOf("ContentDialogResult.Primary", StringComparison.Ordinal);
        var deleteCommandIndex = sourceText.IndexOf("DeleteCommand.ExecuteAsync", StringComparison.Ordinal);

        // Assert
        Assert.IsGreaterThanOrEqualTo(0, handlerIndex, "Expected ImagesPage to define BtnDeleteImage_Click.");
        Assert.IsGreaterThanOrEqualTo(0, dialogIndex, "Expected image deletion to create a confirmation dialog.");
        Assert.IsGreaterThanOrEqualTo(0, primaryResultIndex, "Expected image deletion to require primary confirmation.");
        Assert.IsGreaterThanOrEqualTo(0, deleteCommandIndex, "Expected confirmed image deletion to execute DeleteCommand.");
        Assert.IsTrue(
            handlerIndex < dialogIndex && dialogIndex < deleteCommandIndex,
            "Expected the delete confirmation dialog to be created before executing the delete command.");
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
