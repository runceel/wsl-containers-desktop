namespace WslContainersDesktop_App_Tests.Pages;

/// <summary>
/// DashboardPage の XAML / コードビハインド / MainWindow / App 配線を、WinUIを起動せずに
/// リポジトリのソースファイルを読んで検証するテスト（既存 *PageSourceTests と同方式）。
/// </summary>
[TestClass]
public sealed class DashboardPageSourceTests
{
    [TestMethod]
    public void DashboardPage_XamlShowsLoadingIndicatorWhileStatsLoading()
    {
        // Arrange
        // Refresh 中に Stats 一覧が一瞬空になり「対象なし」が点滅するのを避けるため、
        // 読み込み中は ProgressRing を表示し、一覧の空状態表示は抑制する。
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\DashboardPage.xaml");

        // Assert
        StringAssert.Contains(sourceText, "ProgressRing");
        StringAssert.Contains(sourceText, "IsStatsLoading");
    }

    [TestMethod]
    public void DashboardPage_XamlDisplaysCountsAndStatsFields()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\DashboardPage.xaml");

        // Assert
        StringAssert.Contains(sourceText, "RunningContainerCount");
        StringAssert.Contains(sourceText, "ImageCount");
        StringAssert.Contains(sourceText, "VolumeCount");
        StringAssert.Contains(sourceText, "NetworkCount");
        StringAssert.Contains(sourceText, "CpuText");
        StringAssert.Contains(sourceText, "MemoryText");
    }

    [TestMethod]
    public void DashboardPage_XamlBindsRefreshAndNavigationCommands()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\DashboardPage.xaml");

        // Assert
        StringAssert.Contains(sourceText, "ViewModel.RefreshCommand");
        StringAssert.Contains(sourceText, "ViewModel.ShowContainersCommand");
        StringAssert.Contains(sourceText, "ViewModel.ShowImagesCommand");
        StringAssert.Contains(sourceText, "ViewModel.ShowVolumesCommand");
        StringAssert.Contains(sourceText, "ViewModel.ShowNetworksCommand");
    }

    [TestMethod]
    public void DashboardPage_CodeBehindDelegatesStatsRowActionsToOpenContainerCommands()
    {
        // Arrange
        // 行アクション（詳細/ログ）は、仮想化された ListView.ItemTemplate 内では
        // ページのコマンドへ x:Bind できないため、既存の ContainersPage と同様に
        // Click ハンドラからViewModelのコマンドへ委譲する。
        var codeBehindText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\DashboardPage.xaml.cs");

        // Assert
        StringAssert.Contains(codeBehindText, "OpenContainerDetailsCommand.ExecuteAsync");
        StringAssert.Contains(codeBehindText, "OpenContainerLogsCommand.ExecuteAsync");
    }

    [TestMethod]
    public void DashboardPage_XamlHasStatsEmptyStateAndErrorBindings()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\DashboardPage.xaml");

        // Assert
        StringAssert.Contains(sourceText, "IsStatsEmpty");
        StringAssert.Contains(sourceText, "StatsErrorMessage");
    }

    [TestMethod]
    public void DashboardPage_XamlShowsContainerCountErrorOnBothContainerCards()
    {
        // Arrange
        // 稼働中/停止中の件数は同一のコンテナ取得を共有するため、取得失敗時のエラーは
        // 両方のカードに表示する。片方だけだと、失敗した停止中カードが「未取得」と区別できない。
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\DashboardPage.xaml");

        // Act
        var errorBindingCount = CountOccurrences(sourceText, "ViewModel.ContainerCountErrorMessage");
        var errorVisibilityCount = CountOccurrences(sourceText, "ViewModel.IsContainerCountErrorVisible");

        // Assert
        Assert.IsGreaterThanOrEqualTo(
            2,
            errorBindingCount,
            "Expected the shared container-count error message to be shown on both the running and stopped cards.");
        Assert.IsGreaterThanOrEqualTo(
            2,
            errorVisibilityCount,
            "Expected the shared container-count error visibility to gate both the running and stopped cards.");
    }

    [TestMethod]
    public void DashboardPage_XamlAssignsAutomationIdsToStatsRowActionButtons()
    {
        // Arrange
        // 仮想化された行の詳細/ログボタンにも、行の ContainerId から一意な AutomationId を付与する
        // （既存の ContainersPage と同じ AutomationIdConverter パターン）。
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\DashboardPage.xaml");

        // Assert
        StringAssert.Contains(sourceText, "AutomationIdConverter");
        StringAssert.Contains(sourceText, "ConverterParameter=BtnDashboardStatsDetails");
        StringAssert.Contains(sourceText, "ConverterParameter=BtnDashboardStatsLogs");
    }

    [TestMethod]
    public void DashboardPage_StatsListViewRowsStretchHorizontally()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\DashboardPage.xaml");
        var themeResourcesText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Themes\AppThemeResources.xaml");
        var statsListViewText = ExtractRegion(sourceText, "x:Name=\"LstDashboardStats\"", "</ListView>");
        var statsRowTemplateText = ExtractRegion(statsListViewText, "x:DataType=\"vm:DashboardContainerStatsRowViewModel\"", "</DataTemplate>");

        // Assert
        StringAssert.Contains(themeResourcesText, "x:Key=\"TableListViewItemStyle\"");
        StringAssert.Contains(themeResourcesText, "Property=\"HorizontalAlignment\" Value=\"Stretch\"");
        StringAssert.Contains(themeResourcesText, "Property=\"HorizontalContentAlignment\" Value=\"Stretch\"");
        StringAssert.Contains(themeResourcesText, "Property=\"Padding\" Value=\"0\"");
        StringAssert.Contains(statsListViewText, "ItemContainerStyle=\"{StaticResource TableListViewItemStyle}\"");
        StringAssert.Contains(statsRowTemplateText, "HorizontalAlignment=\"Stretch\"");
        Assert.IsGreaterThanOrEqualTo(
            2,
            CountOccurrences(statsListViewText, "<ColumnDefinition Width=\"192\" />"),
            "Expected the stats header and row to reserve the same fixed action-button column width.");
    }

    [TestMethod]
    public void DashboardPage_CodeBehindResolvesViewModelFromServices()
    {
        // Arrange
        var codeBehindText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\DashboardPage.xaml.cs");

        // Assert
        StringAssert.Contains(codeBehindText, "GetRequiredService<DashboardViewModel>");
    }

    [TestMethod]
    public void DashboardPage_LoadedInvokesRefreshCommand()
    {
        // Arrange
        var codeBehindText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\DashboardPage.xaml.cs");

        // Act
        var loadedIndex = codeBehindText.IndexOf("Loaded +=", StringComparison.Ordinal);
        var refreshIndex = codeBehindText.IndexOf("RefreshCommand.ExecuteAsync", StringComparison.Ordinal);

        // Assert
        Assert.IsGreaterThanOrEqualTo(0, loadedIndex, "Expected DashboardPage to subscribe to the Loaded event.");
        Assert.IsGreaterThanOrEqualTo(0, refreshIndex, "Expected DashboardPage to execute RefreshCommand.");
        Assert.IsTrue(
            loadedIndex < refreshIndex,
            "Expected the Loaded handler to be wired before it executes RefreshCommand.");
    }

    [TestMethod]
    public void MainWindow_ContainsDashboardNavigationItemAndMapping()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\MainWindow.xaml");
        var codeBehindText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\MainWindow.xaml.cs");

        // Assert
        StringAssert.Contains(sourceText, "AutomationProperties.AutomationId=\"NavItemDashboard\"");
        StringAssert.Contains(codeBehindText, "[NavItemDashboard] = NavigationPageKey.Dashboard");
    }

    [TestMethod]
    public void MainWindow_ResolvesNavigationViewModelFromServices()
    {
        // Arrange
        var codeBehindText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\MainWindow.xaml.cs");

        // Assert
        StringAssert.Contains(codeBehindText, "GetRequiredService<NavigationViewModel>");
        Assert.IsFalse(
            codeBehindText.Contains("new NavigationViewModel(", StringComparison.Ordinal),
            "Expected MainWindow to resolve NavigationViewModel from DI instead of constructing it directly.");
    }

    [TestMethod]
    public void App_RegistersNavigationAndDashboardViewModelsAsSingletons()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\App.xaml.cs");

        // Assert
        StringAssert.Contains(sourceText, "AddSingleton<NavigationViewModel>");
        StringAssert.Contains(sourceText, "AddSingleton<DashboardViewModel>");
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

    private static string ExtractRegion(string text, string startMarker, string endMarker)
    {
        var startIndex = text.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, startIndex, $"Expected to find start marker '{startMarker}'.");

        var endIndex = text.IndexOf(endMarker, startIndex, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, endIndex, $"Expected to find end marker '{endMarker}' after '{startMarker}'.");

        return text[startIndex..(endIndex + endMarker.Length)];
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
