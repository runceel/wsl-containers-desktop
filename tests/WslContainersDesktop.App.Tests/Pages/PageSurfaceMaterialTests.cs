namespace WslContainersDesktop_App_Tests.Pages;

[TestClass]
public sealed class PageSurfaceMaterialTests
{
    // Content surfaces are drawn over the window's Mica backdrop. Fluent guidance recommends
    // LayerFillColorDefaultBrush (a low-opacity layer fill) for these surfaces so they pick up
    // the Mica material instead of covering it with an opaque card fill. Each DataRow records the
    // number of layer surfaces a page is expected to render, guarding both the material choice
    // and the completeness of the migration without a brittle full-file ban on other brushes.
    private const string LayerSurfaceBackground = "Background=\"{ThemeResource LayerFillColorDefaultBrush}\"";
    private const string CardSurfaceBackground = "Background=\"{ThemeResource CardBackgroundFillColorDefaultBrush}\"";

    [TestMethod]
    [DataRow(@"src\WslContainersDesktop.App\Pages\ContainersPage.xaml", 6)]
    [DataRow(@"src\WslContainersDesktop.App\Pages\ImagesPage.xaml", 2)]
    [DataRow(@"src\WslContainersDesktop.App\Pages\NetworksPage.xaml", 3)]
    [DataRow(@"src\WslContainersDesktop.App\Pages\VolumesPage.xaml", 2)]
    [DataRow(@"src\WslContainersDesktop.App\Pages\SettingsPage.xaml", 4)]
    public void Page_ContentSurfaces_UseLayerFillOverMica(string relativePath, int expectedSurfaceCount)
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(relativePath);

        // Act
        var layerSurfaceCount = CountOccurrences(sourceText, LayerSurfaceBackground);
        var cardSurfaceCount = CountOccurrences(sourceText, CardSurfaceBackground);

        // Assert
        Assert.AreEqual(
            expectedSurfaceCount,
            layerSurfaceCount,
            $"Expected '{relativePath}' to render {expectedSurfaceCount} content surface(s) with LayerFillColorDefaultBrush over Mica.");
        Assert.AreEqual(
            0,
            cardSurfaceCount,
            $"Expected '{relativePath}' to have migrated every content surface off CardBackgroundFillColorDefaultBrush.");
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
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
