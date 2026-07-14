// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace WslContainersDesktop_App_Tests.Composition;

// Architecture fitness test for ADR-0017: the app composition root should register focused runtime clients directly in the DI container.
[TestClass]
public sealed class RuntimeClientRegistrationSourceTests
{
    [TestMethod]
    public void App_ConfigureServices_RegistersFocusedRuntimeClientsAsSingletons()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\App.xaml.cs");

        // Assert
        StringAssert.Contains(sourceText, "services.AddSingleton<IContainerQueryClient, WslcCliContainerQueryClient>();");
        StringAssert.Contains(sourceText, "services.AddSingleton<IContainerLifecycleClient, WslcCliContainerLifecycleClient>();");
        StringAssert.Contains(sourceText, "services.AddSingleton<IContainerLogClient, WslcCliContainerLogClient>();");
        StringAssert.Contains(sourceText, "services.AddSingleton<IContainerExecClient, WslcCliContainerExecClient>();");
        StringAssert.Contains(sourceText, "services.AddSingleton<IContainerStatsClient, WslcCliContainerStatsClient>();");
        StringAssert.Contains(sourceText, "services.AddSingleton<IImageRuntimeClient, WslcCliImageRuntimeClient>();");
        StringAssert.Contains(sourceText, "services.AddSingleton<IVolumeRuntimeClient, WslcCliVolumeRuntimeClient>();");
        StringAssert.Contains(sourceText, "services.AddSingleton<INetworkRuntimeClient, WslcCliNetworkRuntimeClient>();");
    }

    [TestMethod]
    public void App_ConfigureServices_DoesNotRegisterLegacyRuntimeClient()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\App.xaml.cs");

        // Assert
        Assert.IsFalse(sourceText.Contains("IContainerRuntimeClient", StringComparison.Ordinal), "Expected the app composition root to avoid referencing the legacy runtime port.");
        Assert.IsFalse(sourceText.Contains("WslcCliContainerRuntimeClient", StringComparison.Ordinal), "Expected the app composition root to avoid referencing the legacy runtime client.");
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
