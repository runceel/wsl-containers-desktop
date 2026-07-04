using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WslContainersDesktop_App_Tests.Themes;

[TestClass]
public sealed class AppThemeResourcesTests
{
    [TestMethod]
    public void AppThemeResources_ThemeDictionaries_DefineSupportedThemeKeys()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Themes\AppThemeResources.xaml");

        // Act
        // No-op: the test is validating the XAML dictionary keys declared in the source file.

        // Assert
        StringAssert.Contains(sourceText, "x:Key=\"Default\"");
        StringAssert.Contains(sourceText, "x:Key=\"Light\"");
        StringAssert.Contains(sourceText, "x:Key=\"HighContrast\"");
        Assert.IsFalse(sourceText.Contains("x:Key=\"Dark\"", StringComparison.Ordinal), "Expected the theme resource XAML to avoid a Dark theme dictionary entry.");
    }

    [TestMethod]
    public void AppThemeResources_ThemeDictionaries_DefineExpectedResourceKeys()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Themes\AppThemeResources.xaml");
        var expectedResourceKeys = new[]
        {
            "WslContainersAccentFillColorDefaultBrush",
            "WslContainersAccentFillColorSubtleBrush",
            "WslContainersAccentStrokeColorBrush",
            "WslContainersAccentTextFillColorBrush",
            "WslContainersSurfaceBorderThickness",
        };

        // Act
        // No-op: the test is validating the XAML resource keys declared in the source file.

        // Assert
        foreach (var expectedResourceKey in expectedResourceKeys)
        {
            StringAssert.Contains(sourceText, expectedResourceKey, $"Expected theme resource key '{expectedResourceKey}' to be declared in the theme resource XAML.");
        }
    }

    [TestMethod]
    public void AppThemeResources_HighContrastDictionary_UsesSystemColorBrushes()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Themes\AppThemeResources.xaml");
        var match = Regex.Match(sourceText, @"<ResourceDictionary\s+x:Key=""HighContrast"".*?</ResourceDictionary>", RegexOptions.Singleline);

        // Act
        // No-op: the test is validating the structure of the HighContrast dictionary section.

        // Assert
        Assert.IsTrue(match.Success, "Expected the theme resource XAML to define a HighContrast ResourceDictionary.");

        var highContrastSection = match.Value;
        StringAssert.Contains(highContrastSection, "SystemColorHighlightColorBrush");
        StringAssert.Contains(highContrastSection, "SystemColorHighlightTextColorBrush");
        StringAssert.Contains(highContrastSection, "SystemColorWindowColorBrush");
        StringAssert.Contains(highContrastSection, "SystemColorWindowTextColorBrush");
        Assert.IsFalse(highContrastSection.Contains("#107C10", StringComparison.Ordinal), "Expected the HighContrast dictionary to avoid the app green hex literal '#107C10'.");
        Assert.IsFalse(highContrastSection.Contains("#0F6B0F", StringComparison.Ordinal), "Expected the HighContrast dictionary to avoid the app green hex literal '#0F6B0F'.");
        Assert.IsFalse(highContrastSection.Contains("#13A10E", StringComparison.Ordinal), "Expected the HighContrast dictionary to avoid the app green hex literal '#13A10E'.");
        Assert.IsFalse(highContrastSection.Contains("Color=\"#", StringComparison.Ordinal), "Expected the HighContrast dictionary to avoid inline Color values with hex literals.");
    }

    [TestMethod]
    public void AppXaml_MergesAppThemeResources()
    {
        // Arrange
        var appXamlText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\App.xaml");

        // Act
        // No-op: the test is validating the App.xaml merge dictionary declaration.

        // Assert
        Assert.IsTrue(
            appXamlText.Contains("Themes/AppThemeResources.xaml", StringComparison.Ordinal) ||
            appXamlText.Contains("Themes\\AppThemeResources.xaml", StringComparison.Ordinal),
            "Expected App.xaml to merge the AppThemeResources.xaml file from the Themes folder.");
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
