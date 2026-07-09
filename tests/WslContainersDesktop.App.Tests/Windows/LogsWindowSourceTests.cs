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
    public void LogsWindow_Header_HasPauseResumeAndClearCommands()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Windows\LogsWindow.xaml");

        // Assert: ポップアウト側からもPause/Resume/Clearを操作できること
        // (rubber-duckレビュー指摘: ポップアウト側にセッション停止手段が必要)。
        StringAssert.Contains(sourceText, "Command=\"{x:Bind ViewModel.PauseLogsCommand}\"");
        StringAssert.Contains(sourceText, "Command=\"{x:Bind ViewModel.ResumeLogsCommand}\"");
        StringAssert.Contains(sourceText, "Command=\"{x:Bind ViewModel.ClearLogsCommand}\"");
    }

    [TestMethod]
    public void LogsWindow_CloseButton_UsesClickHandlerInsteadOfCloseLogsCommand()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Windows\LogsWindow.xaml");

        // Assert: Closeボタンはウィンドウを閉じるだけの操作にする(ログ追跡自体は継続する)ため、
        // セッション停止用のCloseLogsCommandではなくコードビハインドのClickハンドラに配線する。
        StringAssert.Contains(sourceText, "x:Name=\"BtnCloseLogs\"");
        StringAssert.Contains(sourceText, "Click=\"BtnCloseLogs_Click\"");
        Assert.IsFalse(
            sourceText.Contains("Command=\"{x:Bind ViewModel.CloseLogsCommand}\""),
            "BtnCloseLogsはウィンドウを閉じるだけにするため、CloseLogsCommandへのバインドを持ってはいけない。");
    }

    [TestMethod]
    public void LogsWindowCodeBehind_CloseButtonClickHandler_ClosesWindow()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Windows\LogsWindow.xaml.cs");

        // Assert: Clickハンドラが実際にWindow.Close()を呼び出していること
        // (ボタンの存在確認だけでは押下時に何もしない実装を見逃すため)。
        StringAssert.Contains(sourceText, "BtnCloseLogs_Click");
        StringAssert.Contains(sourceText, "Close();");
    }

    [TestMethod]
    public void LogsWindow_TitleBar_MatchesMainWindowLookAndFeel()
    {
        // Arrange
        var xamlSourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Windows\LogsWindow.xaml");
        var codeBehindSourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Windows\LogsWindow.xaml.cs");

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

    [TestMethod]
    public void LogsWindow_RootGrid_HasNameForLoadedEventSubscription()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Windows\LogsWindow.xaml");

        // Assert: DPIリサイズはRootGridのLoadedイベントで行う(Activatedはウィンドウ再生成時に
        // XamlRoot確立前に発火しNullReferenceExceptionを起こすため使わない。再現・詳細は
        // LogsWindowCodeBehind_DoesNotResizeOnActivated_ToAvoidXamlRootRaceConditionテスト参照)。
        StringAssert.Contains(sourceText, "x:Name=\"RootGrid\"");
    }

    [TestMethod]
    public void LogsWindowCodeBehind_DoesNotResizeOnActivated_ToAvoidXamlRootRaceConditionBug()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Windows\LogsWindow.xaml.cs");

        // Assert: 「Logsウィンドウを閉じて再度開くとアプリがクラッシュする」バグの再発防止。
        // SingleInstanceWindowOpenerで2回目以降に生成されるLogsWindowはActivate()呼び出しから
        // Activatedイベント発火までの間にXamlRootが未確立のことがあり、
        // Content.XamlRoot.RasterizationScaleへの直接アクセスがNullReferenceExceptionで
        // クラッシュしていた(実機で再現確認済み)。ActivatedではなくLoadedイベントを使い、
        // XamlRoot確立後にのみリサイズすることでこれを防ぐ。
        Assert.IsFalse(
            sourceText.Contains("Activated += LogsWindow_Activated;"),
            "ActivatedイベントでのDPIリサイズはXamlRoot未確立によるクラッシュを再発させるため使用禁止。");
        StringAssert.Contains(sourceText, "RootGrid.Loaded +=");
    }

    [TestMethod]
    public void LogsWindowCodeBehind_LoadedHandler_ReadsXamlRootFromRootGridNotContent()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Windows\LogsWindow.xaml.cs");

        // Assert: Loadedイベントの時点ではRootGrid.XamlRootが確立済みであることを保証された
        // 状態で読み取る(Content.XamlRootへの無条件アクセスを再導入しない)。
        StringAssert.Contains(sourceText, "RootGrid.XamlRoot.RasterizationScale");
        Assert.IsFalse(
            sourceText.Contains("Content.XamlRoot.RasterizationScale"),
            "Content.XamlRootへの無条件アクセスはXamlRoot未確立時にクラッシュするため使用禁止。");
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
