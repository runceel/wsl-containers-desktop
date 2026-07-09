namespace WslContainersDesktop_App_Tests.Pages;

[TestClass]
public sealed class ContainersPageSourceTests
{
    [TestMethod]
    public void ContainersPage_StateColumnBindsDisplayStateOneWay()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ContainersPage.xaml");

        // Act
        // No-op: the test validates the source binding required by the intermediate-state display feature.

        // Assert
        StringAssert.Contains(
            sourceText,
            "Text=\"{x:Bind DisplayState, Mode=OneWay, Converter={StaticResource StateToDisplayTextConverter}}\"",
            "State列は途中状態（Stopping等）を反映するため DisplayState を Mode=OneWay でバインドする必要がある。");
    }

    [TestMethod]
    public void ContainersPage_ListViewItemsUseSharedTableStyle()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ContainersPage.xaml");

        // Assert
        StringAssert.Contains(sourceText, "ItemContainerStyle=\"{StaticResource TableListViewItemStyle}\"");
        StringAssert.Contains(sourceText, "<Grid Padding=\"12,8\" ColumnSpacing=\"12\" HorizontalAlignment=\"Stretch\">");
    }

    [TestMethod]
    public void ContainersPage_LogPanelHeader_HasOpenLogsWindowButton()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ContainersPage.xaml");

        // Assert: ログパネルを大きな個別ウィンドウで開くボタンが存在し、コードビハインドの
        // Clickハンドラに配線されていること。
        StringAssert.Contains(sourceText, "x:Name=\"BtnOpenLogsWindow\"");
        StringAssert.Contains(sourceText, "Click=\"BtnOpenLogsWindow_Click\"");
    }

    [TestMethod]
    public void ContainersPage_ShellPanelHeader_HasOpenShellWindowButton()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ContainersPage.xaml");

        // Assert: シェルパネルを大きな個別ウィンドウで開くボタンが存在し、コードビハインドの
        // Clickハンドラに配線されていること。
        StringAssert.Contains(sourceText, "x:Name=\"BtnOpenShellWindow\"");
        StringAssert.Contains(sourceText, "Click=\"BtnOpenShellWindow_Click\"");
    }

    [TestMethod]
    public void ContainersPageCodeBehind_OpenLogsWindowClickHandler_DelegatesToAuxiliaryWindowManager()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ContainersPage.xaml.cs");

        // Assert: ボタンが存在するだけでなく、実際にContainerAuxiliaryWindowManager経由で
        // ログウィンドウを開く呼び出しへ配線されていること(rubber-duckレビュー指摘:
        // ボタンの存在確認だけでは押下時に何もしない実装や誤配線を見逃す)。
        StringAssert.Contains(sourceText, "BtnOpenLogsWindow_Click");
        StringAssert.Contains(sourceText, "_windowManager.ShowLogsWindow()");
    }

    [TestMethod]
    public void ContainersPageCodeBehind_OpenShellWindowClickHandler_DelegatesToAuxiliaryWindowManager()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ContainersPage.xaml.cs");

        // Assert: ボタンが存在するだけでなく、実際にContainerAuxiliaryWindowManager経由で
        // シェルウィンドウを開く呼び出しへ配線されていること。
        StringAssert.Contains(sourceText, "BtnOpenShellWindow_Click");
        StringAssert.Contains(sourceText, "_windowManager.ShowShellWindow()");
    }

    [TestMethod]
    public void ContainersPage_RowTemplate_HasInlineStartButtonBoundToCanStart()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ContainersPage.xaml");

        // Assert
        StringAssert.Contains(
            sourceText,
            "x:Uid=\"BtnStart\"",
            "よく使う操作であるStartは、行のトップレベルにインラインボタンとして表示する必要がある。");
        StringAssert.Contains(
            sourceText,
            "Visibility=\"{x:Bind CanStart, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}\"",
            "インラインのStartボタンは、実行可能なとき(CanStart)のみ表示する必要がある。");

        var startButtonMarkup = ExtractElementMarkup(sourceText, "x:Uid=\"BtnStart\"");
        StringAssert.Contains(
            startButtonMarkup,
            "Click=\"BtnStart_Click\"",
            "インラインのStartボタンはクリック時にStartCommandへ配線されている必要がある。");
        StringAssert.Contains(
            startButtonMarkup,
            "CommandParameter=\"{x:Bind}\"",
            "BtnStartのCommandParameterには行のContainerRowViewModel自身({x:Bind})を渡す必要がある。");
    }

    [TestMethod]
    public void ContainersPage_RowTemplate_HasInlineStopButtonBoundToCanStop()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ContainersPage.xaml");

        // Assert
        StringAssert.Contains(
            sourceText,
            "x:Uid=\"BtnStop\"",
            "よく使う操作であるStopは、行のトップレベルにインラインボタンとして表示する必要がある。");
        StringAssert.Contains(
            sourceText,
            "Visibility=\"{x:Bind CanStop, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}\"",
            "インラインのStopボタンは、実行可能なとき(CanStop)のみ表示する必要がある。");

        var stopButtonMarkup = ExtractElementMarkup(sourceText, "x:Uid=\"BtnStop\"");
        StringAssert.Contains(
            stopButtonMarkup,
            "Click=\"BtnStop_Click\"",
            "インラインのStopボタンはクリック時にStopCommandへ配線されている必要がある。");
        StringAssert.Contains(
            stopButtonMarkup,
            "CommandParameter=\"{x:Bind}\"",
            "BtnStopのCommandParameterには行のContainerRowViewModel自身({x:Bind})を渡す必要がある。");
    }

    [TestMethod]
    public void ContainersPage_MoreActionsMenu_DoesNotContainStartOrStopItems()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ContainersPage.xaml");

        // Assert
        // Start/StopはBtnStart/BtnStopのインラインボタンに置き換わったため、
        // "..."メニュー(MenuFlyout)内に重複した項目を残してはならない。
        StringAssert.DoesNotMatch(sourceText, new System.Text.RegularExpressions.Regex("x:Uid=\"MenuStart\""));
        StringAssert.DoesNotMatch(sourceText, new System.Text.RegularExpressions.Regex("x:Uid=\"MenuStop\""));
    }

    [TestMethod]
    public void ContainersPage_MoreActionsMenu_StillContainsRestartAndDeleteItemsWithVisibilityBinding()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ContainersPage.xaml");

        // Assert
        // Restart/Deleteは実行頻度が低いため、引き続き"..."メニューの中に残し、
        // 実行不可能なときは非表示にする既存の挙動を維持する。
        StringAssert.Contains(sourceText, "x:Uid=\"MenuRestart\"");
        StringAssert.Contains(
            sourceText,
            "Visibility=\"{x:Bind CanRestart, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}\"");
        StringAssert.Contains(sourceText, "x:Uid=\"MenuDelete\"");
        StringAssert.Contains(
            sourceText,
            "Visibility=\"{x:Bind CanDelete, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}\"");
    }

    [TestMethod]
    public void ContainersPage_ActionColumnWidth_MatchesBetweenHeaderAndRowTemplate()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ContainersPage.xaml");

        // Assert
        // Start/Stop/…の3ボタンを収めるため、ヘッダー行とデータ行テンプレートの両方で
        // アクション列(最終列)の幅を"220"に揃える必要がある(片方だけ変更されるとズレる)。
        var actionColumnWidthOccurrences = System.Text.RegularExpressions.Regex.Matches(
            sourceText,
            "<ColumnDefinition Width=\"220\" />").Count;
        Assert.AreEqual(
            2,
            actionColumnWidthOccurrences,
            "アクション列の幅(220px)はヘッダー行・データ行テンプレートの両方に定義されている必要がある。");
    }

    private static string ExtractElementMarkup(string sourceText, string marker)
    {
        var markerIndex = sourceText.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsTrue(markerIndex >= 0, $"Expected to find marker '{marker}' in the source text.");

        var elementEndIndex = sourceText.IndexOf("/>", markerIndex, StringComparison.Ordinal);
        Assert.IsTrue(elementEndIndex >= 0, $"Expected to find a self-closing element after marker '{marker}'.");

        return sourceText[markerIndex..elementEndIndex];
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
