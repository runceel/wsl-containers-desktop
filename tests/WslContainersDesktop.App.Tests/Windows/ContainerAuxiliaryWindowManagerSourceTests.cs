// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace WslContainersDesktop_App_Tests.Windows;

[TestClass]
public sealed class ContainerAuxiliaryWindowManagerSourceTests
{
    [TestMethod]
    public void App_ConfigureServices_RegistersContainerAuxiliaryWindowManagerAsSingleton()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\App.xaml.cs");

        // Assert: LogsWindow/ShellWindowはContainersPageのNavigateのたびに参照が切れないよう、
        // アプリケーションライフタイムのSingletonとして登録されている必要がある
        // (rubber-duckレビュー指摘: ウィンドウ参照をページのフィールドで持つと追跡が切れる)。
        StringAssert.Contains(sourceText, "services.AddSingleton<ContainerAuxiliaryWindowManager>();");
    }

    [TestMethod]
    public void ContainerAuxiliaryWindowManager_ComposesSingleInstanceWindowOpenersForLogsAndShellWindows()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Windows\ContainerAuxiliaryWindowManager.cs");

        // Assert: 生成/再利用/Closed後再生成のロジック本体は独立してテスト済みの
        // SingleInstanceWindowOpener<TWindow>に委譲しており、本クラスはLogsWindow/ShellWindow
        // それぞれのopenerを合成するだけであること。
        StringAssert.Contains(sourceText, "new SingleInstanceWindowOpener<LogsWindow>(");
        StringAssert.Contains(sourceText, "new SingleInstanceWindowOpener<ShellWindow>(");
        StringAssert.Contains(sourceText, "public void ShowLogsWindow() => _logsWindowOpener.ShowOrActivate();");
        StringAssert.Contains(sourceText, "public void ShowShellWindow() => _shellWindowOpener.ShowOrActivate();");
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
