// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace WslContainersDesktop_App_Tests.Windows;

[TestClass]
public sealed class LogsWindowSourceTests
{
    [TestMethod]
    public void LogsWindow_ListView_BindsToViewModelLogLinesWithAutoScroll()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Windows\LogsWindow.xaml");

        // Assert: 小さいログパネルと同じViewModel.LogLinesを共有し、末尾自動スクロールの
        // Inverted listsパターン(ItemsUpdatingScrollMode="KeepLastItemInView")も踏襲する。
        StringAssert.Contains(sourceText, "ItemsSource=\"{x:Bind ViewModel.LogLines, Mode=OneWay}\"");
        StringAssert.Contains(sourceText, "ItemsUpdatingScrollMode=\"KeepLastItemInView\"");
    }

    [TestMethod]
    public void LogsWindow_Header_HasPauseResumeClearAndCloseCommands()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Windows\LogsWindow.xaml");

        // Assert: ポップアウト側からもPause/Resume/Clear/Closeを操作できること
        // (rubber-duckレビュー指摘: ポップアウト側にセッション停止手段が必要)。
        StringAssert.Contains(sourceText, "Command=\"{x:Bind ViewModel.PauseLogsCommand}\"");
        StringAssert.Contains(sourceText, "Command=\"{x:Bind ViewModel.ResumeLogsCommand}\"");
        StringAssert.Contains(sourceText, "Command=\"{x:Bind ViewModel.ClearLogsCommand}\"");
        StringAssert.Contains(sourceText, "Command=\"{x:Bind ViewModel.CloseLogsCommand}\"");
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
