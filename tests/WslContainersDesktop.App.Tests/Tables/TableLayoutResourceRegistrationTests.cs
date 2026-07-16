using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using WslContainersDesktop_App.Tables;

namespace WslContainersDesktop_App_Tests.Tables;

[TestClass]
[DoNotParallelize]
public sealed class TableLayoutResourceRegistrationTests
{
    [UITestMethod]
    public void RegisterResources_EmptyDictionary_AddsAllFiveExactKeys()
    {
        // Arrange
        var resources = new ResourceDictionary();

        // Act
        TableColumnLayoutCatalog.RegisterResources(resources);

        // Assert
        Assert.AreEqual(5, resources.Count);
        Assert.IsTrue(resources.ContainsKey(TableColumnLayoutCatalog.ContainersResourceKey));
        Assert.IsTrue(resources.ContainsKey(TableColumnLayoutCatalog.ImagesResourceKey));
        Assert.IsTrue(resources.ContainsKey(TableColumnLayoutCatalog.VolumesResourceKey));
        Assert.IsTrue(resources.ContainsKey(TableColumnLayoutCatalog.NetworksResourceKey));
        Assert.IsTrue(resources.ContainsKey(TableColumnLayoutCatalog.DashboardStatsResourceKey));

        AssertLayoutMatchesPreset(GetLayout(resources, TableColumnLayoutCatalog.ContainersResourceKey), TableLayoutPreset.Containers);
        AssertLayoutMatchesPreset(GetLayout(resources, TableColumnLayoutCatalog.ImagesResourceKey), TableLayoutPreset.Images);
        AssertLayoutMatchesPreset(GetLayout(resources, TableColumnLayoutCatalog.VolumesResourceKey), TableLayoutPreset.Volumes);
        AssertLayoutMatchesPreset(GetLayout(resources, TableColumnLayoutCatalog.NetworksResourceKey), TableLayoutPreset.Networks);
        AssertLayoutMatchesPreset(GetLayout(resources, TableColumnLayoutCatalog.DashboardStatsResourceKey), TableLayoutPreset.DashboardStats);
    }

    [UITestMethod]
    public void RegisterResources_AllLayouts_AreDistinctInstances()
    {
        // Arrange
        var resources = new ResourceDictionary();

        // Act
        TableColumnLayoutCatalog.RegisterResources(resources);
        var containersLayout = GetLayout(resources, TableColumnLayoutCatalog.ContainersResourceKey);
        var imagesLayout = GetLayout(resources, TableColumnLayoutCatalog.ImagesResourceKey);
        var volumesLayout = GetLayout(resources, TableColumnLayoutCatalog.VolumesResourceKey);
        var networksLayout = GetLayout(resources, TableColumnLayoutCatalog.NetworksResourceKey);
        var dashboardStatsLayout = GetLayout(resources, TableColumnLayoutCatalog.DashboardStatsResourceKey);

        // Assert
        Assert.AreNotSame(containersLayout, imagesLayout);
        Assert.AreNotSame(containersLayout, volumesLayout);
        Assert.AreNotSame(containersLayout, networksLayout);
        Assert.AreNotSame(containersLayout, dashboardStatsLayout);
        Assert.AreNotSame(imagesLayout, volumesLayout);
        Assert.AreNotSame(imagesLayout, networksLayout);
        Assert.AreNotSame(imagesLayout, dashboardStatsLayout);
        Assert.AreNotSame(volumesLayout, networksLayout);
        Assert.AreNotSame(volumesLayout, dashboardStatsLayout);
        Assert.AreNotSame(networksLayout, dashboardStatsLayout);
    }

    [UITestMethod]
    public void RegisterResources_CalledAgain_PreservesExistingInstancesAndCurrentWidths()
    {
        // Arrange
        var resources = new ResourceDictionary();
        TableColumnLayoutCatalog.RegisterResources(resources);

        var containersLayout = GetLayout(resources, TableColumnLayoutCatalog.ContainersResourceKey);
        containersLayout.SetWidth(1, new GridLength(240d, GridUnitType.Pixel));

        var imagesLayout = GetLayout(resources, TableColumnLayoutCatalog.ImagesResourceKey);
        var volumesLayout = GetLayout(resources, TableColumnLayoutCatalog.VolumesResourceKey);
        var networksLayout = GetLayout(resources, TableColumnLayoutCatalog.NetworksResourceKey);
        var dashboardStatsLayout = GetLayout(resources, TableColumnLayoutCatalog.DashboardStatsResourceKey);

        // Act
        TableColumnLayoutCatalog.RegisterResources(resources);

        // Assert
        var reRegisteredContainersLayout = GetLayout(resources, TableColumnLayoutCatalog.ContainersResourceKey);
        Assert.AreSame(containersLayout, reRegisteredContainersLayout);
        Assert.AreEqual(240d, reRegisteredContainersLayout.GetWidth(1).Value);
        Assert.AreEqual(GridUnitType.Pixel, reRegisteredContainersLayout.GetWidth(1).GridUnitType);

        Assert.AreSame(imagesLayout, resources[TableColumnLayoutCatalog.ImagesResourceKey]);
        Assert.AreSame(volumesLayout, resources[TableColumnLayoutCatalog.VolumesResourceKey]);
        Assert.AreSame(networksLayout, resources[TableColumnLayoutCatalog.NetworksResourceKey]);
        Assert.AreSame(dashboardStatsLayout, resources[TableColumnLayoutCatalog.DashboardStatsResourceKey]);
    }

    [UITestMethod]
    public void RegisterResources_SeparateDictionary_CreatesFreshDefaultInstances()
    {
        // Arrange
        var firstResources = new ResourceDictionary();
        TableColumnLayoutCatalog.RegisterResources(firstResources);

        var firstContainersLayout = GetLayout(firstResources, TableColumnLayoutCatalog.ContainersResourceKey);
        firstContainersLayout.SetWidth(1, new GridLength(240d, GridUnitType.Pixel));

        var secondResources = new ResourceDictionary();

        // Act
        TableColumnLayoutCatalog.RegisterResources(secondResources);

        // Assert
        var secondContainersLayout = GetLayout(secondResources, TableColumnLayoutCatalog.ContainersResourceKey);
        Assert.AreNotSame(firstContainersLayout, secondContainersLayout);
        Assert.AreEqual(GridUnitType.Pixel, secondContainersLayout.GetWidth(1).GridUnitType);
        Assert.AreEqual(130d, secondContainersLayout.GetWidth(1).Value);
    }

    [UITestMethod]
    public void RegisterResources_Null_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() => TableColumnLayoutCatalog.RegisterResources(null!));
    }

    [TestMethod]
    public void App_OnLaunched_RegistersTableLayoutsBeforeConstructingMainWindow()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\App.xaml.cs");
        var expectedCall = "TableColumnLayoutCatalog.RegisterResources(Resources);";

        // Act
        var constructorBody = ExtractMethodBody(sourceText, "public App()");
        var launchedBody = ExtractMethodBody(sourceText, "protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)");
        var registrationCallCount = CountOccurrences(sourceText, expectedCall);
        var registrationCallIndex = launchedBody.IndexOf(expectedCall, StringComparison.Ordinal);
        var mainWindowCreationIndex = launchedBody.IndexOf("new MainWindow()", StringComparison.Ordinal);

        // Assert
        Assert.AreEqual(1, registrationCallCount, "Expected a single registration call in App.xaml.cs.");
        Assert.IsFalse(constructorBody.Contains(expectedCall), "Expected the constructor body to not contain the registration call.");
        Assert.IsFalse(constructorBody.Contains("Resources"), "Expected the constructor body to not access Resources.");
        Assert.IsTrue(launchedBody.Contains(expectedCall), "Expected the OnLaunched body to contain the registration call.");
        Assert.IsGreaterThanOrEqualTo(0, registrationCallIndex, "Expected the registration call to be present in the OnLaunched body.");
        Assert.IsGreaterThanOrEqualTo(0, mainWindowCreationIndex, "Expected MainWindow construction to be present in the OnLaunched body.");
        Assert.IsLessThan(upperBound: mainWindowCreationIndex, value: registrationCallIndex, "Expected the registration call to occur before constructing MainWindow().");
    }

    private static void AssertLayoutMatchesPreset(TableColumnLayout layout, TableLayoutPreset preset)
    {
        var expectedLayout = TableColumnLayoutCatalog.Create(preset);

        Assert.IsNotNull(layout);
        Assert.AreEqual(expectedLayout.ColumnCount, layout.ColumnCount);
        Assert.AreEqual(expectedLayout.ActionRailWidth, layout.ActionRailWidth);

        for (var index = 0; index < expectedLayout.ColumnCount; index++)
        {
            var expectedWidth = expectedLayout.GetWidth(index);
            var actualWidth = layout.GetWidth(index);

            Assert.AreEqual(expectedWidth.GridUnitType, actualWidth.GridUnitType);
            Assert.AreEqual(expectedWidth.Value, actualWidth.Value);
            Assert.AreEqual(expectedLayout.GetMinWidth(index), layout.GetMinWidth(index));
            Assert.AreEqual(expectedLayout.GetMaxWidth(index), layout.GetMaxWidth(index));
        }
    }

    private static TableColumnLayout GetLayout(ResourceDictionary resources, string resourceKey)
    {
        return (TableColumnLayout)resources[resourceKey];
    }

    private static string ExtractMethodBody(string sourceText, string methodSignature)
    {
        var methodSignatureIndex = sourceText.IndexOf(methodSignature, StringComparison.Ordinal);
        Assert.IsTrue(methodSignatureIndex >= 0, $"Expected to find method signature '{methodSignature}' in App.xaml.cs.");

        var openingBraceIndex = sourceText.IndexOf('{', methodSignatureIndex);
        Assert.IsTrue(openingBraceIndex >= 0, $"Expected to find opening brace for method signature '{methodSignature}'.");

        var braceDepth = 0;
        for (var index = openingBraceIndex; index < sourceText.Length; index++)
        {
            var currentCharacter = sourceText[index];
            if (currentCharacter == '{')
            {
                braceDepth++;
            }
            else if (currentCharacter == '}')
            {
                braceDepth--;
                if (braceDepth == 0)
                {
                    return sourceText.Substring(openingBraceIndex + 1, index - openingBraceIndex - 1);
                }
            }
        }

        Assert.Fail($"Could not extract body for method signature '{methodSignature}'.");
        return string.Empty;
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
