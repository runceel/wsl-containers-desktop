using System.Text.RegularExpressions;

namespace WslContainersDesktop_App_Tests.Pages;

[TestClass]
public sealed class NetworksPageSourceTests
{
    [TestMethod]
    public void NetworksPage_XamlDisplaysRequiredNetworkFields()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\NetworksPage.xaml");

        // Assert
        StringAssert.Contains(sourceText, "Text=\"{x:Bind Name}\"");
        StringAssert.Contains(sourceText, "Text=\"{x:Bind Driver}\"");
        StringAssert.Contains(sourceText, "Text=\"{x:Bind CreatedAtText}\"");
        StringAssert.Contains(sourceText, "Text=\"{x:Bind ConnectedContainerCountText}\"");
        StringAssert.Contains(sourceText, "Text=\"{x:Bind TypeText}\"");
    }

    [TestMethod]
    public void NetworksPage_HeaderIncludesAccentBar()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\NetworksPage.xaml");

        // Assert
        StringAssert.Contains(sourceText, "<ColumnDefinition Width=\"4\" />");
        StringAssert.Contains(sourceText, "Background=\"{ThemeResource WslContainersAccentFillColorDefaultBrush}\"");
        StringAssert.Contains(sourceText, "CornerRadius=\"{StaticResource ControlCornerRadius}\"");
    }

    [TestMethod]
    public void NetworksPage_ListViewItemsStretchHorizontally()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\NetworksPage.xaml");

        // Assert
        StringAssert.Contains(sourceText, "ItemContainerStyle=\"{StaticResource TableListViewItemStyle}\"");
        StringAssert.Contains(sourceText, "HorizontalAlignment=\"Stretch\"");
    }

    [TestMethod]
    public void NetworksPage_CreateProgressRing_IsBottomAligned()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\NetworksPage.xaml");
        var ringPattern = new Regex(
            """
            <ProgressRing\b(?=[^>]*?x:Name="PrgCreateNetwork")(?=[^>]*?VerticalAlignment="Bottom")[^>]*?/>
            """);

        // Assert
        Assert.IsTrue(
            ringPattern.IsMatch(sourceText),
            "Expected the PrgCreateNetwork progress ring to be bottom-aligned so it lines up with the create button.");
    }

    [TestMethod]
    public void NetworksPage_NoNetworks_EmptyStateUsesDistinguishableSurfaceBorder()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\NetworksPage.xaml");
        var emptyStateBorderPattern = new Regex(
            """
            <Border\b[^>]*x:Name="BorderNetworksEmptyState"[^>]*Background="\{ThemeResource LayerFillColorDefaultBrush\}"[^>]*BorderBrush="\{ThemeResource CardStrokeColorDefaultBrush\}"[^>]*BorderThickness="\{ThemeResource WslContainersSurfaceBorderThickness\}"[^>]*CornerRadius="\{StaticResource OverlayCornerRadius\}"[^>]*\sVisibility="\{x:Bind ToVisibleWhenFalse\(ViewModel\.HasNetworks\), Mode=OneWay\}"[^>]*>(?s:.*?)<TextBlock\b[^>]*x:Name="TxtNetworksEmptyState"
            """,
            RegexOptions.Singleline);

        // Assert
        Assert.IsTrue(
            emptyStateBorderPattern.IsMatch(sourceText),
            "Expected the empty-state section to be a named Border with a distinguishable layer surface, a card stroke, and the visible-when-false binding.");
    }

    [TestMethod]
    public void NetworksPage_DeleteClickShowsConfirmationBeforeExecutingDeleteCommand()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\NetworksPage.xaml.cs");

        // Act
        var handlerIndex = sourceText.IndexOf("BtnDeleteNetwork_Click", StringComparison.Ordinal);
        var dialogIndex = sourceText.IndexOf("ContentDialog", StringComparison.Ordinal);
        var primaryResultIndex = sourceText.IndexOf("ContentDialogResult.Primary", StringComparison.Ordinal);
        var deleteCommandIndex = sourceText.IndexOf("DeleteCommand.ExecuteAsync", StringComparison.Ordinal);

        // Assert
        Assert.IsGreaterThanOrEqualTo(0, handlerIndex, "Expected NetworksPage to define BtnDeleteNetwork_Click.");
        Assert.IsGreaterThanOrEqualTo(0, dialogIndex, "Expected network deletion to create a confirmation dialog.");
        Assert.IsGreaterThanOrEqualTo(0, primaryResultIndex, "Expected network deletion to require primary confirmation.");
        Assert.IsGreaterThanOrEqualTo(0, deleteCommandIndex, "Expected confirmed network deletion to execute DeleteCommand.");
        Assert.IsTrue(
            handlerIndex < dialogIndex && dialogIndex < deleteCommandIndex,
            "Expected the delete confirmation dialog to be created before executing the delete command.");
    }

    [TestMethod]
    public void MainWindow_SourceContainsNetworksNavigationItemAndMapping()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\MainWindow.xaml");
        var codeBehindText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\MainWindow.xaml.cs");

        // Assert
        StringAssert.Contains(sourceText, "AutomationProperties.AutomationId=\"NavItemNetworks\"");
        StringAssert.Contains(codeBehindText, "[NavItemNetworks] = NavigationPageKey.Networks");
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
