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
    public void ShellWindow_Footer_HasCommandInputAndSendCommand()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Windows\ShellWindow.xaml");

        // Assert: ポップアウト側からもコマンド送信ができること
        // (rubber-duckレビュー指摘: ポップアウト側にセッション停止手段が必要)。
        StringAssert.Contains(sourceText, "Text=\"{x:Bind ViewModel.ShellCommandText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"");
        StringAssert.Contains(sourceText, "Command=\"{x:Bind ViewModel.SendShellCommandCommand}\"");
    }

    [TestMethod]
    public void ShellWindow_CloseButton_UsesClickHandlerInsteadOfCloseShellCommand()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Windows\ShellWindow.xaml");

        // Assert: Closeボタンはウィンドウを閉じるだけの操作にする(シェルセッション自体は継続する)ため、
        // セッション停止用のCloseShellCommandではなくコードビハインドのClickハンドラに配線する。
        StringAssert.Contains(sourceText, "x:Name=\"BtnCloseShell\"");
        StringAssert.Contains(sourceText, "Click=\"BtnCloseShell_Click\"");
        Assert.IsFalse(
            sourceText.Contains("Command=\"{x:Bind ViewModel.CloseShellCommand}\""),
            "BtnCloseShellはウィンドウを閉じるだけにするため、CloseShellCommandへのバインドを持ってはいけない。");
    }

    [TestMethod]
    public void ShellWindowCodeBehind_CloseButtonClickHandler_ClosesWindow()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Windows\ShellWindow.xaml.cs");

        // Assert: Clickハンドラが実際にWindow.Close()を呼び出していること
        // (ボタンの存在確認だけでは押下時に何もしない実装を見逃すため)。
        StringAssert.Contains(sourceText, "BtnCloseShell_Click");
        StringAssert.Contains(sourceText, "Close();");
    }

    [TestMethod]
    public void ShellWindow_TitleBar_MatchesMainWindowLookAndFeel()
    {
        // Arrange
        var xamlSourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Windows\ShellWindow.xaml");
        var codeBehindSourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Windows\ShellWindow.xaml.cs");

        // Assert: MainWindowと同じ、アプリアイコン付きのカスタムTitleBarを
        // ExtendsContentIntoTitleBar + Tall高さで表示する。
        StringAssert.Contains(xamlSourceText, "<TitleBar");
        StringAssert.Contains(xamlSourceText, "IsPaneToggleButtonVisible=\"False\"");
        StringAssert.Contains(xamlSourceText, "ImageSource=\"Assets/AppIcon.ico\"");
        StringAssert.Contains(codeBehindSourceText, "ExtendsContentIntoTitleBar = true;");
        StringAssert.Contains(codeBehindSourceText, "TitleBarHeightOption.Tall");
        StringAssert.Contains(codeBehindSourceText, "SetTitleBar(");
        StringAssert.Contains(codeBehindSourceText, "AppWindow.SetIcon(\"Assets/AppIcon.ico\");");
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
