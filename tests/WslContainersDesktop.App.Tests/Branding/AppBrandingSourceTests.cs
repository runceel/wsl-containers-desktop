using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WslContainersDesktop_App_Tests.Branding;

[TestClass]
public sealed class AppBrandingSourceTests
{
    private const string ExpectedAppName = "Hakonexa - WSL Containers Manager";
    private const string ExpectedSettingsPurpose = "Manage WSL Containers from a native Windows desktop app.";

    [TestMethod]
    public void AppBranding_Resources_UseExpectedDisplayNameValues()
    {
        // Arrange
        var resourceDocument = XDocument.Parse(ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Strings\en-US\Resources.resw"));

        // Act
        var mainWindowTitle = GetResourceValue(resourceDocument, "MainWindow.Title");
        var appTitleBarTitle = GetResourceValue(resourceDocument, "AppTitleBar.Title");
        var appDisplayName = GetResourceValue(resourceDocument, "AppDisplayName");
        var appDescription = GetResourceValue(resourceDocument, "AppDescription");

        // Assert
        Assert.AreEqual(ExpectedAppName, mainWindowTitle, "Expected MainWindow.Title to use the renamed app display name.");
        Assert.AreEqual(ExpectedAppName, appTitleBarTitle, "Expected AppTitleBar.Title to use the renamed app display name.");
        Assert.AreEqual(ExpectedAppName, appDisplayName, "Expected AppDisplayName to use the renamed app display name.");
        Assert.AreEqual(ExpectedAppName, appDescription, "Expected AppDescription to use the renamed app display name.");
    }

    [TestMethod]
    public void AppBranding_SettingsPage_UsesLocalizedAppNameAndPurposeResources()
    {
        // Arrange
        var resourceDocument = XDocument.Parse(ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Strings\en-US\Resources.resw"));
        var settingsPageXamlText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\SettingsPage.xaml");

        // Act
        var settingsAppNameText = GetResourceValue(resourceDocument, "SettingsAppName.Text");
        var settingsAppPurposeText = GetResourceValue(resourceDocument, "SettingsAppPurpose.Text");

        // Assert
        Assert.AreEqual(ExpectedAppName, settingsAppNameText, "Expected SettingsAppName.Text to use the renamed app display name.");
        Assert.AreEqual(ExpectedSettingsPurpose, settingsAppPurposeText, "Expected SettingsAppPurpose.Text to use the updated app purpose message.");
        StringAssert.Contains(settingsPageXamlText, "x:Uid=\"SettingsAppName\"");
        StringAssert.Contains(settingsPageXamlText, "x:Uid=\"SettingsAppPurpose\"");
        Assert.IsFalse(settingsPageXamlText.Contains(ExpectedAppName, StringComparison.Ordinal), "Expected SettingsPage.xaml to avoid hardcoding the app name.");
    }

    [TestMethod]
    public void AppBranding_Manifest_UsesResourceReferencesForDisplayNameAndDescription()
    {
        // Arrange
        var manifestText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Package.appxmanifest");

        // Assert
        StringAssert.Contains(manifestText, "<DisplayName>ms-resource:AppDisplayName</DisplayName>");
        StringAssert.Contains(manifestText, "DisplayName=\"ms-resource:AppDisplayName\"");
        StringAssert.Contains(manifestText, "Description=\"ms-resource:AppDescription\"");
    }

    [TestMethod]
    public void AppBranding_Readme_UsesExpectedAppTitleHeading()
    {
        // Arrange
        var readmeText = ReadRepositorySourceFile("README.md");

        // Assert
        StringAssert.Contains(readmeText, "# Hakonexa - WSL Containers Manager");
    }

    private static string GetResourceValue(XDocument resourceDocument, string resourceName)
    {
        var dataElement = resourceDocument.Root?
            .Elements("data")
            .FirstOrDefault(element => string.Equals((string?)element.Attribute("name"), resourceName, StringComparison.Ordinal));

        Assert.IsNotNull(dataElement, $"Expected resource key '{resourceName}' to exist in the resources file.");
        var valueElement = dataElement!.Element("value");
        Assert.IsNotNull(valueElement, $"Expected resource key '{resourceName}' to contain a <value> element.");

        return valueElement!.Value;
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
