// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace WslContainersDesktop_App_Tests.Windows;

[TestClass]
public sealed class ShellWindowSourceTests
{
    [TestMethod]
    public void ShellWindow_ListView_BindsToViewModelShellOutput()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Windows\ShellWindow.xaml");

        // Assert: 小さいシェルパネルと同じViewModel.ShellOutputを共有する。
        StringAssert.Contains(sourceText, "ItemsSource=\"{x:Bind ViewModel.ShellOutput, Mode=OneWay}\"");
    }

    [TestMethod]
    public void ShellWindow_Footer_HasCommandInputSendAndCloseCommands()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Windows\ShellWindow.xaml");

        // Assert: ポップアウト側からもコマンド送信・シェル切断ができること
        // (rubber-duckレビュー指摘: ポップアウト側にセッション停止手段が必要)。
        StringAssert.Contains(sourceText, "Text=\"{x:Bind ViewModel.ShellCommandText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"");
        StringAssert.Contains(sourceText, "Command=\"{x:Bind ViewModel.SendShellCommandCommand}\"");
        StringAssert.Contains(sourceText, "Command=\"{x:Bind ViewModel.CloseShellCommand}\"");
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
