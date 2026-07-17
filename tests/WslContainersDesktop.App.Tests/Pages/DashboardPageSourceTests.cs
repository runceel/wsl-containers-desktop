using System.Linq;
using System.Xml.Linq;

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
    public void DashboardPage_StatsTableUsesHeaderSiblingAndTableColumnSplitters()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\DashboardPage.xaml");
        var resourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Strings\en-US\Resources.resw");
        var xamlDocument = XDocument.Parse(sourceText);
        var xamlNamespace = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
        var tablesNamespaceDeclaration = xamlDocument.Root?.Attributes()
            .SingleOrDefault(attribute => attribute.IsNamespaceDeclaration && attribute.Name.LocalName == "tables");

        // Assert
        Assert.IsNotNull(tablesNamespaceDeclaration, "Expected the Dashboard page root to declare the tables prefix.");
        Assert.AreEqual(
            "using:WslContainersDesktop_App.Tables",
            tablesNamespaceDeclaration!.Value,
            "Expected the Dashboard page root to declare the tables prefix with the reviewed namespace.");
        Assert.IsFalse(sourceText.Contains("CommunityToolkit", StringComparison.OrdinalIgnoreCase), "The Dashboard page should not reference CommunityToolkit concrete splitter types.");

        var tablesNamespace = XNamespace.Get(tablesNamespaceDeclaration!.Value);
        var presentationNamespace = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml/presentation");
        var statsListView = FindElementByXamlName(xamlDocument.Root!, "LstDashboardStats");
        Assert.IsNull(
            statsListView.Element(presentationNamespace + "ListView.HeaderTemplate"),
            "Expected LstDashboardStats to have no direct ListView.HeaderTemplate element.");

        var header = FindImmediatePreviousElementSibling(statsListView);
        var dataTemplateRoot = FindDataTemplateRoot(statsListView);
        var statsHost = header.Parent!;
        var headerColumns = ReadColumnWidths(header, presentationNamespace);
        var rowColumns = ReadColumnWidths(dataTemplateRoot, presentationNamespace);
        var rowDefinitions = statsHost.Elements(presentationNamespace + "Grid.RowDefinitions").SingleOrDefault();
        Assert.IsNotNull(rowDefinitions, "Expected the stats table host to define a direct Grid.RowDefinitions property element.");
        var rowDefinitionHeights = rowDefinitions!.Elements(presentationNamespace + "RowDefinition").Select(element => element.Attribute("Height")?.Value ?? string.Empty).ToList();

        Assert.AreEqual("0", GetRequiredAttributeValue(header, "Grid.Row"));
        Assert.AreEqual("1", GetRequiredAttributeValue(statsListView, "Grid.Row"));
        Assert.AreEqual(2, rowDefinitionHeights.Count, "Expected the stats table host to define two rows for header and list content.");
        Assert.AreEqual("Auto", rowDefinitionHeights[0]);
        Assert.AreEqual("*", rowDefinitionHeights[1]);
        Assert.AreEqual("{x:Bind ToVisibleWhenFalse(ViewModel.IsStatsLoading), Mode=OneWay}", GetRequiredAttributeValue(header, "Visibility"));
        Assert.AreEqual("{x:Bind ToVisibleWhenFalse(ViewModel.IsStatsLoading), Mode=OneWay}", GetRequiredAttributeValue(statsListView, "Visibility"));
        Assert.AreEqual("8,4", GetRequiredAttributeValue(header, "Padding"));
        Assert.AreEqual("8", GetRequiredAttributeValue(dataTemplateRoot, "Padding"));
        Assert.AreEqual("12", GetRequiredAttributeValue(header, "ColumnSpacing"));
        Assert.AreEqual("12", GetRequiredAttributeValue(dataTemplateRoot, "ColumnSpacing"));
        Assert.AreEqual("Stretch", GetRequiredAttributeValue(header, "HorizontalAlignment"));
        Assert.AreEqual("Stretch", GetRequiredAttributeValue(dataTemplateRoot, "HorizontalAlignment"));
        CollectionAssert.AreEqual(new[] { "*", "192" }, headerColumns.ToArray(), "The header outer grid should use the reviewed metadata/action column widths.");
        CollectionAssert.AreEqual(new[] { "*", "192" }, rowColumns.ToArray(), "The row template outer grid should use the reviewed metadata/action column widths.");

        var headerMetadataGrid = FindDirectChildByGridColumn(header, "0");
        var rowMetadataGrid = FindDirectChildByGridColumn(dataTemplateRoot, "0");
        Assert.AreEqual("True", GetRequiredAttributeValue(headerMetadataGrid, tablesNamespace + "ClipToBoundsBehavior.IsEnabled"));
        Assert.AreEqual("True", GetRequiredAttributeValue(rowMetadataGrid, tablesNamespace + "ClipToBoundsBehavior.IsEnabled"));
        Assert.AreEqual("Header", GetRequiredAttributeValue(headerMetadataGrid, tablesNamespace + "TableLayoutBehavior.Role"));
        Assert.AreEqual("Row", GetRequiredAttributeValue(rowMetadataGrid, tablesNamespace + "TableLayoutBehavior.Role"));
        Assert.AreEqual("{StaticResource DashboardStatsTableColumnLayout}", GetRequiredAttributeValue(headerMetadataGrid, tablesNamespace + "TableLayoutBehavior.Layout"));
        Assert.AreEqual("{StaticResource DashboardStatsTableColumnLayout}", GetRequiredAttributeValue(rowMetadataGrid, tablesNamespace + "TableLayoutBehavior.Layout"));

        AssertMetadataGridColumns(headerMetadataGrid, presentationNamespace, tablesNamespace, "header");
        AssertMetadataGridColumns(rowMetadataGrid, presentationNamespace, tablesNamespace, "row");

        var headerSplitters = headerMetadataGrid.Elements(tablesNamespace + "TableColumnSplitter").ToList();
        var rowSplitters = rowMetadataGrid.Elements(tablesNamespace + "TableColumnSplitter").ToList();
        Assert.AreEqual(2, headerSplitters.Count, "Expected the header metadata grid to define two direct TableColumnSplitter elements.");
        Assert.AreEqual(0, rowSplitters.Count, "Expected the row metadata grid to have no direct TableColumnSplitter elements.");

        var expectedHeaderSplitters = new[]
        {
            (Uid: "SpltDashboardStatsNameCpu", Column: "1"),
            (Uid: "SpltDashboardStatsCpuMemory", Column: "3")
        };

        foreach (var expectedHeaderSplitter in expectedHeaderSplitters)
        {
            var splitter = headerSplitters.SingleOrDefault(element => (string?)element.Attribute(xamlNamespace + "Uid") == expectedHeaderSplitter.Uid);
            Assert.IsNotNull(splitter, $"Expected header splitter '{expectedHeaderSplitter.Uid}' to exist.");
            Assert.AreEqual(expectedHeaderSplitter.Column, GetRequiredAttributeValue(splitter!, "Grid.Column"));
            Assert.AreEqual(expectedHeaderSplitter.Uid, GetRequiredAttributeValue(splitter!, "AutomationProperties.AutomationId"));
            Assert.AreEqual(expectedHeaderSplitter.Uid, GetRequiredAttributeValue(splitter!, xamlNamespace + "Uid"));
            Assert.AreEqual("Stretch", GetRequiredAttributeValue(splitter!, "HorizontalAlignment"));
        }

        var actionHost = FindDirectChildByGridColumn(dataTemplateRoot, "1");
        var detailsButton = FindButtonByUid(actionHost, "BtnDashboardStatsDetails");
        var logsButton = FindButtonByUid(actionHost, "BtnDashboardStatsLogs");
        Assert.AreEqual("BtnStatsDetails_Click", GetRequiredAttributeValue(detailsButton, "Click"));
        Assert.AreEqual("{x:Bind}", GetRequiredAttributeValue(detailsButton, "CommandParameter"));
        Assert.AreEqual(
            "{x:Bind ContainerId, Mode=OneTime, Converter={StaticResource AutomationIdConverter}, ConverterParameter=BtnDashboardStatsDetails}",
            GetRequiredAttributeValue(detailsButton, "AutomationProperties.AutomationId"));
        Assert.AreEqual("BtnStatsLogs_Click", GetRequiredAttributeValue(logsButton, "Click"));
        Assert.AreEqual("{x:Bind}", GetRequiredAttributeValue(logsButton, "CommandParameter"));
        Assert.AreEqual(
            "{x:Bind ContainerId, Mode=OneTime, Converter={StaticResource AutomationIdConverter}, ConverterParameter=BtnDashboardStatsLogs}",
            GetRequiredAttributeValue(logsButton, "AutomationProperties.AutomationId"));

        Assert.IsTrue(ContainsElementWithUid(header, "TxtDashboardStatsColumnName"), "Expected the header outer grid to contain the Name column header Uid.");
        Assert.IsTrue(ContainsElementWithUid(header, "TxtDashboardStatsColumnCpu"), "Expected the header outer grid to contain the CPU column header Uid.");
        Assert.IsTrue(ContainsElementWithUid(header, "TxtDashboardStatsColumnMemory"), "Expected the header outer grid to contain the Memory column header Uid.");
        Assert.IsTrue(sourceText.Contains("Text=\"{x:Bind Name}\"", StringComparison.Ordinal), "Expected the row template to preserve the Name binding.");
        Assert.IsTrue(sourceText.Contains("Text=\"{x:Bind CpuText}\"", StringComparison.Ordinal), "Expected the row template to preserve the CpuText binding.");
        Assert.IsTrue(sourceText.Contains("Text=\"{x:Bind MemoryText}\"", StringComparison.Ordinal), "Expected the row template to preserve the MemoryText binding.");
        Assert.IsTrue(sourceText.Contains("ItemContainerStyle=\"{StaticResource TableListViewItemStyle}\"", StringComparison.Ordinal), "Expected the stats ListView to preserve the shared table item style.");
        AssertResourceValue(resourceText, "SpltDashboardStatsNameCpu.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name");
        AssertResourceValue(resourceText, "SpltDashboardStatsCpuMemory.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name");
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

    private static void AssertMetadataGridColumns(
        XElement metadataGrid,
        XNamespace presentationNamespace,
        XNamespace tablesNamespace,
        string gridRole)
    {
        var columnDefinitions = metadataGrid
            .Elements(presentationNamespace + "Grid.ColumnDefinitions")
            .SelectMany(definitions => definitions.Elements(presentationNamespace + "ColumnDefinition"))
            .ToList();

        Assert.AreEqual(
            5,
            columnDefinitions.Count,
            $"Expected the {gridRole} metadata grid to define five interleaved columns for 3 logical columns and 2 splitters.");
        Assert.AreEqual("0", GetRequiredAttributeValue(columnDefinitions[0], tablesNamespace + "TableLayoutBehavior.ColumnIndex"));
        Assert.AreEqual("8", GetRequiredAttributeValue(columnDefinitions[1], "Width"));
        Assert.AreEqual("1", GetRequiredAttributeValue(columnDefinitions[2], tablesNamespace + "TableLayoutBehavior.ColumnIndex"));
        Assert.AreEqual("8", GetRequiredAttributeValue(columnDefinitions[3], "Width"));
        Assert.AreEqual("2", GetRequiredAttributeValue(columnDefinitions[4], tablesNamespace + "TableLayoutBehavior.ColumnIndex"));
    }

    private static bool ContainsElementWithUid(XElement root, string uid)
    {
        return root.Descendants().Any(element => (string?)element.Attribute(XName.Get("{http://schemas.microsoft.com/winfx/2006/xaml}Uid")) == uid);
    }

    private static XElement FindDirectChildByGridColumn(XElement parent, string gridColumnValue)
    {
        var directChild = parent.Elements().SingleOrDefault(element => (string?)element.Attribute("Grid.Column") == gridColumnValue);
        Assert.IsNotNull(directChild, $"Expected '{parent.Name}' to contain a direct child in Grid.Column='{gridColumnValue}'.");
        return directChild!;
    }

    private static XElement FindButtonByUid(XElement container, string uid)
    {
        var button = container.Descendants().SingleOrDefault(element => element.Name.LocalName == "Button" && (string?)element.Attribute(XName.Get("{http://schemas.microsoft.com/winfx/2006/xaml}Uid")) == uid);
        Assert.IsNotNull(button, $"Expected the container to contain a Button with x:Uid='{uid}'.");
        return button!;
    }

    private static XElement FindElementByXamlName(XElement root, string elementName)
    {
        var element = root.Descendants().FirstOrDefault(e => e.Attribute(XName.Get("{http://schemas.microsoft.com/winfx/2006/xaml}Name"))?.Value == elementName);
        Assert.IsNotNull(element, $"Expected to find an element named '{elementName}'.");
        return element!;
    }

    private static XElement FindImmediatePreviousElementSibling(XElement element)
    {
        var parent = element.Parent;
        Assert.IsNotNull(parent, $"Expected '{element.Name}' to have a parent element.");

        XElement? previous = null;
        foreach (var child in parent!.Elements())
        {
            if (child == element)
            {
                break;
            }

            previous = child;
        }

        Assert.IsNotNull(previous, $"Expected '{element.Name}' to have an immediate previous sibling element.");
        return previous!;
    }

    private static XElement FindDataTemplateRoot(XElement listView)
    {
        var presentationNamespace = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml/presentation");
        var itemTemplateName = presentationNamespace + "ListView.ItemTemplate";
        var dataTemplate = listView.Descendants().FirstOrDefault(e => e.Name.LocalName == "DataTemplate" && e.Parent?.Name == itemTemplateName);
        Assert.IsNotNull(dataTemplate, "Expected the stats ListView to contain an ItemTemplate DataTemplate.");

        var root = dataTemplate!.Elements().FirstOrDefault();
        Assert.IsNotNull(root, "Expected the stats DataTemplate to contain a root element.");
        return root!;
    }

    private static IReadOnlyList<string> ReadColumnWidths(XElement element, XNamespace presentationNamespace)
    {
        var columnDefinitions = element.Element(presentationNamespace + "Grid.ColumnDefinitions");
        Assert.IsNotNull(columnDefinitions, $"Expected '{element.Name}' to define column widths.");

        return columnDefinitions!.Elements(presentationNamespace + "ColumnDefinition").Select(e => e.Attribute("Width")?.Value ?? string.Empty).ToList();
    }

    private static string GetRequiredAttributeValue(XElement element, string attributeName)
    {
        return GetRequiredAttributeValue(element, XName.Get(attributeName));
    }

    private static string GetRequiredAttributeValue(XElement element, XName attributeName)
    {
        var attribute = element.Attribute(attributeName);
        Assert.IsNotNull(attribute, $"Expected '{element.Name}' to define attribute '{attributeName}'.");
        return attribute!.Value;
    }

    private static void AssertResourceValue(string resourceText, string resourceName)
    {
        var document = XDocument.Parse(resourceText);
        var resource = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "data" && e.Attribute("name")?.Value == resourceName);
        Assert.IsNotNull(resource, $"Expected resources to contain '{resourceName}'.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(resource!.Value), $"Expected resource '{resourceName}' to be non-empty.");
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
