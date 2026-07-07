using System.Text.RegularExpressions;

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
    public void ImagesPage_RowIncludesRunActionWithAutomationId()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ImagesPage.xaml");

        // Assert
        StringAssert.Contains(sourceText, "BtnRunImage");
        StringAssert.Contains(sourceText, "ConverterParameter=BtnRunImage");
    }

    [TestMethod]
    public void ImagesPage_RunDialogIncludesDetailedFields()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ImagesPage.xaml");

        // Assert
        StringAssert.Contains(sourceText, "TxtRunContainerName");
        StringAssert.Contains(sourceText, "TglRunRemoveWhenStopped");
        StringAssert.Contains(sourceText, "TxtRunPortMappings");
        StringAssert.Contains(sourceText, "TxtRunEnvironmentVariables");
        StringAssert.Contains(sourceText, "TxtRunCommand");
    }

    [TestMethod]
    public void ImagesPage_RunClickShowsDialogBeforeExecutingRunCommand()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ImagesPage.xaml.cs");

        // Act
        var handlerIndex = sourceText.IndexOf("BtnRunImage_Click", StringComparison.Ordinal);
        var handlerSearchStart = Math.Max(handlerIndex, 0);
        var dialogIndex = sourceText.IndexOf("RunImageDialog.ShowAsync", handlerSearchStart, StringComparison.Ordinal);
        var primaryResultIndex = sourceText.IndexOf("ContentDialogResult.Primary", handlerSearchStart, StringComparison.Ordinal);
        var runCommandIndex = sourceText.IndexOf("RunCommand.ExecuteAsync", handlerSearchStart, StringComparison.Ordinal);

        // Assert
        Assert.IsGreaterThanOrEqualTo(0, handlerIndex, "Expected ImagesPage to define BtnRunImage_Click.");
        Assert.IsGreaterThanOrEqualTo(0, dialogIndex, "Expected image run to show the run configuration dialog.");
        Assert.IsGreaterThanOrEqualTo(0, primaryResultIndex, "Expected image run to require primary confirmation.");
        Assert.IsGreaterThanOrEqualTo(0, runCommandIndex, "Expected confirmed image run to execute RunCommand.");
        Assert.IsTrue(
            handlerIndex < dialogIndex && dialogIndex < primaryResultIndex && primaryResultIndex < runCommandIndex,
            "Expected the run configuration dialog to be shown before executing the run command.");
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
        StringAssert.Contains(sourceText, "Background=\"{ThemeResource LayerFillColorDefaultBrush}\"");
    }

    [TestMethod]
    public void ImagesPage_ListViewItemsUseSharedStretchStyle()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ImagesPage.xaml");
        var listSectionText = ExtractRegion(sourceText, "<Grid Grid.Row=\"0\" Padding=\"12,8\" ColumnSpacing=\"12\">", "</ListView>");

        // Assert
        StringAssert.Contains(sourceText, "ItemContainerStyle=\"{StaticResource TableListViewItemStyle}\"");
        StringAssert.Contains(sourceText, "HorizontalAlignment=\"Stretch\"");
        Assert.IsGreaterThanOrEqualTo(
            2,
            CountOccurrences(listSectionText, "<ColumnDefinition Width=\"224\" />"),
            "Expected the image list header and row to reserve the same fixed action-button column width.");
    }

    [TestMethod]
    public void ImagesPage_PullProgressRing_IsBottomAligned()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ImagesPage.xaml");
        var ringPattern = new Regex(
            """
            <ProgressRing\b(?=[^>]*?x:Name="PrgPullImage")(?=[^>]*?VerticalAlignment="Bottom")[^>]*?/>
            """);

        // Assert
        Assert.IsTrue(
            ringPattern.IsMatch(sourceText),
            "Expected the PrgPullImage progress ring to be bottom-aligned so it lines up with the pull button.");
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

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string ExtractRegion(string text, string startMarker, string endMarker)
    {
        var startIndex = text.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, startIndex, $"Expected to find start marker '{startMarker}'.");

        var endIndex = text.IndexOf(endMarker, startIndex, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, endIndex, $"Expected to find end marker '{endMarker}' after '{startMarker}'.");

        return text[startIndex..(endIndex + endMarker.Length)];
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
