using System.Globalization;
using System.Xml.Linq;

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
        var document = XDocument.Parse(sourceText);
        XNamespace presentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

        // Act
        var listView = FindContainersListView(document, presentationNamespace, xamlNamespace);
        var rowTemplateGrid = FindRowTemplateGrid(listView, presentationNamespace);

        // Assert
        Assert.AreEqual(
            "{StaticResource TableListViewItemStyle}",
            (string?)listView.Attribute("ItemContainerStyle"),
            "The Containers page should use the shared table list view item style.");
        Assert.AreEqual(
            "12,8",
            (string?)rowTemplateGrid.Attribute("Padding"),
            "The row template grid should use the shared table row padding values.");
        Assert.AreEqual(
            "12",
            (string?)rowTemplateGrid.Attribute("ColumnSpacing"),
            "The row template grid should use the shared table column spacing.");
        Assert.AreEqual(
            "Stretch",
            (string?)rowTemplateGrid.Attribute("HorizontalAlignment"),
            "The row template grid should stretch horizontally to fill the table layout.");
    }

    [TestMethod]
    public void ContainersPage_SourceUsesReviewedHeaderAndRowTableLayoutContract()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ContainersPage.xaml");
        var document = XDocument.Parse(sourceText);
        XNamespace presentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
        var tablesNamespace = document.Root?.GetNamespaceOfPrefix("tables");

        // Assert
        Assert.IsNotNull(tablesNamespace, "Expected the Containers page XAML to declare the tables namespace.");
        Assert.AreEqual(
            "using:WslContainersDesktop_App.Tables",
            tablesNamespace!.NamespaceName,
            "The Containers page XAML should declare the tables namespace for table layout helpers.");

        var listView = FindContainersListView(document, presentationNamespace, xamlNamespace);
        var headerGrid = FindHeaderGrid(listView, presentationNamespace);
        var rowTemplateGrid = FindRowTemplateGrid(listView, presentationNamespace);

        CollectionAssert.AreEqual(
            new[] { "*", "96" },
            headerGrid
                .Element(presentationNamespace + "Grid.ColumnDefinitions")?
                .Elements(presentationNamespace + "ColumnDefinition")
                .Select(element => (string?)element.Attribute("Width"))
                .ToArray(),
            "The header outer grid should use the reviewed metadata/action column widths.");
        CollectionAssert.AreEqual(
            new[] { "*", "96" },
            rowTemplateGrid
                .Element(presentationNamespace + "Grid.ColumnDefinitions")?
                .Elements(presentationNamespace + "ColumnDefinition")
                .Select(element => (string?)element.Attribute("Width"))
                .ToArray(),
            "The row template outer grid should use the reviewed metadata/action column widths.");

        var headerMetadataGrid = FindMetadataGrid(headerGrid);
        var rowMetadataGrid = FindMetadataGrid(rowTemplateGrid);
        AssertMetadataGridContract(headerMetadataGrid, presentationNamespace, tablesNamespace, "Header", expectedSplitterCount: 3);
        AssertMetadataGridContract(rowMetadataGrid, presentationNamespace, tablesNamespace, "Row", expectedSplitterCount: 0);

        var headerSplitters = headerMetadataGrid.Elements(tablesNamespace + "TableColumnSplitter").ToList();
        Assert.AreEqual(3, headerSplitters.Count, "The header metadata grid should expose three table column splitters at the boundaries.");
        var expectedSplitterUids = new[] { "SpltContainersNameImage", "SpltContainersImageState", "SpltContainersStateCreated" };
        foreach (var expectedSplitterUid in expectedSplitterUids)
        {
            var splitter = headerSplitters.SingleOrDefault(element => (string?)element.Attribute(xamlNamespace + "Uid") == expectedSplitterUid);
            Assert.IsNotNull(splitter, $"Expected header splitter '{expectedSplitterUid}' to exist.");
            Assert.AreEqual(
                expectedSplitterUid,
                (string?)splitter!.Attribute("AutomationProperties.AutomationId"),
                $"Expected splitter '{expectedSplitterUid}' to use the reviewed automation id.");
        }

        Assert.IsFalse(sourceText.Contains("CommunityToolkit", StringComparison.OrdinalIgnoreCase), "The page should not reference CommunityToolkit concrete splitter types.");
    }

    [TestMethod]
    public void ContainersPage_Resources_UseLocalizedUidKeysForActionButtonsAndSplitters()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Strings\en-US\Resources.resw");
        var document = XDocument.Parse(sourceText);

        // Assert
        Assert.IsNull(FindReswDataEntry(document, "BtnStart.Content"), "The resources should not use BtnStart.Content for the inline Start button.");
        Assert.IsNull(FindReswDataEntry(document, "BtnStop.Content"), "The resources should not use BtnStop.Content for the inline Stop button.");

        var expectedLocalizedValues = new[]
        {
            (Name: "BtnStart.[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip", ExpectedValue: "Start"),
            (Name: "BtnStart.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", ExpectedValue: (string?)null),
            (Name: "BtnStop.[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip", ExpectedValue: "Stop"),
            (Name: "BtnStop.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", ExpectedValue: (string?)null),
            (Name: "BtnMoreActions.[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip", ExpectedValue: (string?)null),
            (Name: "BtnMoreActions.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", ExpectedValue: (string?)null),
            (Name: "SpltContainersNameImage.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", ExpectedValue: (string?)null),
            (Name: "SpltContainersImageState.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", ExpectedValue: (string?)null),
            (Name: "SpltContainersStateCreated.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", ExpectedValue: (string?)null),
        };

        foreach (var (name, expectedValue) in expectedLocalizedValues)
        {
            var entry = FindReswDataEntry(document, name);
            Assert.IsNotNull(entry, $"Expected the resources to contain '{name}'.");
            var value = entry!.Element("value")?.Value;
            Assert.IsFalse(string.IsNullOrWhiteSpace(value), $"Expected '{name}' to have a nonempty localized value.");
            if (expectedValue is not null)
            {
                Assert.AreEqual(expectedValue, value, $"Expected '{name}' to use the reviewed localized value.");
            }
        }
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
    public void ContainersPage_RowTemplate_HasActionRailWithStartAndStopButtonsUsingReviewedBindings()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ContainersPage.xaml");
        var document = XDocument.Parse(sourceText);
        XNamespace presentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

        // Act
        var listView = FindContainersListView(document, presentationNamespace, xamlNamespace);
        var rowTemplateGrid = FindRowTemplateGrid(listView, presentationNamespace);
        var actionHost = FindActionHost(rowTemplateGrid);
        var startButton = FindButton(actionHost, presentationNamespace, xamlNamespace, "BtnStart");
        var stopButton = FindButton(actionHost, presentationNamespace, xamlNamespace, "BtnStop");

        // Assert
        Assert.IsNotNull(actionHost, "The row template should define an action host in the outer Grid.Column=1 slot.");
        Assert.AreEqual(
            "{x:Bind IsStartActionVisible, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}",
            (string?)startButton.Attribute("Visibility"),
            "The Start button should use the reviewed IsStartActionVisible visibility binding.");
        Assert.AreEqual(
            "{x:Bind CanStart, Mode=OneWay}",
            (string?)startButton.Attribute("IsEnabled"),
            "The Start button should be enabled through the reviewed CanStart OneWay binding.");
        Assert.AreEqual(
            "BtnStart_Click",
            (string?)startButton.Attribute("Click"),
            "The Start button should preserve the reviewed Click handler.");
        Assert.AreEqual(
            "{x:Bind}",
            (string?)startButton.Attribute("CommandParameter"),
            "The Start button should preserve the reviewed CommandParameter binding.");
        Assert.IsNotNull(
            startButton.Descendants(presentationNamespace + "SymbolIcon").SingleOrDefault(element => (string?)element.Attribute("Symbol") == "Play"),
            "The Start button should contain a Play SymbolIcon.");
        Assert.IsTrue(
            double.TryParse((string?)startButton.Attribute("MinWidth"), out var startMinWidth) && startMinWidth >= 40,
            "The Start button should have a MinWidth of at least 40.");
        Assert.IsTrue(
            double.TryParse((string?)startButton.Attribute("MinHeight"), out var startMinHeight) && startMinHeight >= 40,
            "The Start button should have a MinHeight of at least 40.");

        Assert.AreEqual(
            "{x:Bind IsStopActionVisible, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}",
            (string?)stopButton.Attribute("Visibility"),
            "The Stop button should use the reviewed IsStopActionVisible visibility binding.");
        Assert.AreEqual(
            "{x:Bind CanStop, Mode=OneWay}",
            (string?)stopButton.Attribute("IsEnabled"),
            "The Stop button should be enabled through the reviewed CanStop OneWay binding.");
        Assert.AreEqual(
            "BtnStop_Click",
            (string?)stopButton.Attribute("Click"),
            "The Stop button should preserve the reviewed Click handler.");
        Assert.AreEqual(
            "{x:Bind}",
            (string?)stopButton.Attribute("CommandParameter"),
            "The Stop button should preserve the reviewed CommandParameter binding.");
        Assert.IsNotNull(
            stopButton.Descendants(presentationNamespace + "SymbolIcon").SingleOrDefault(element => (string?)element.Attribute("Symbol") == "Stop"),
            "The Stop button should contain a Stop SymbolIcon.");
        Assert.IsTrue(
            double.TryParse((string?)stopButton.Attribute("MinWidth"), out var stopMinWidth) && stopMinWidth >= 40,
            "The Stop button should have a MinWidth of at least 40.");
        Assert.IsTrue(
            double.TryParse((string?)stopButton.Attribute("MinHeight"), out var stopMinHeight) && stopMinHeight >= 40,
            "The Stop button should have a MinHeight of at least 40.");
        Assert.IsNotNull(
            FindButton(actionHost, presentationNamespace, xamlNamespace, "BtnMoreActions"),
            "The row template should keep the More Actions button in the action rail.");
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
        var (headerGrid, rowTemplateGrid) = FindContainersListGrids(sourceText);

        // Assert
        var headerActionColumnWidth = GetFinalColumnWidth(headerGrid, "header");
        var rowActionColumnWidth = GetFinalColumnWidth(rowTemplateGrid, "row template");

        Assert.AreEqual(
            headerActionColumnWidth,
            rowActionColumnWidth,
            "The header and row template action columns must have the same width.");
    }

    private static (XElement HeaderGrid, XElement RowTemplateGrid) FindContainersListGrids(string sourceText)
    {
        var document = XDocument.Parse(sourceText);
        XNamespace presentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
        var listView = FindContainersListView(document, presentationNamespace, xamlNamespace);
        return (
            FindHeaderGrid(listView, presentationNamespace),
            FindRowTemplateGrid(listView, presentationNamespace));
    }

    private static double GetFinalColumnWidth(XElement grid, string gridDescription)
    {
        XNamespace presentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var actionColumn = grid
            .Element(presentationNamespace + "Grid.ColumnDefinitions")?
            .Elements(presentationNamespace + "ColumnDefinition")
            .LastOrDefault();
        Assert.IsNotNull(actionColumn, $"Expected the {gridDescription} Grid to define an action column.");

        var width = (string?)actionColumn.Attribute("Width");
        Assert.IsTrue(
            double.TryParse(width, NumberStyles.Number, CultureInfo.InvariantCulture, out var numericWidth),
            $"Expected the {gridDescription} action column to have a numeric Width, but found '{width ?? "(missing)"}'.");

        return numericWidth;
    }

    private static XElement? FindReswDataEntry(XDocument document, string name)
    {
        return document
            .Descendants("data")
            .SingleOrDefault(element => (string?)element.Attribute("name") == name);
    }

    private static XElement FindContainersListView(XDocument document, XNamespace presentationNamespace, XNamespace xamlNamespace)
    {
        var listView = document
            .Descendants(presentationNamespace + "ListView")
            .SingleOrDefault(element => (string?)element.Attribute(xamlNamespace + "Name") == "LstContainers");
        Assert.IsNotNull(listView, "Expected ContainersPage to define the LstContainers ListView.");
        return listView!;
    }

    private static XElement FindHeaderGrid(XElement listView, XNamespace presentationNamespace)
    {
        var headerGrid = listView.ElementsBeforeSelf().LastOrDefault();
        Assert.IsNotNull(headerGrid, "Expected LstContainers to have a preceding header element.");
        Assert.AreEqual(
            presentationNamespace + "Grid",
            headerGrid!.Name,
            "Expected LstContainers' immediately preceding sibling to be the column-header Grid.");
        return headerGrid;
    }

    private static XElement FindRowTemplateGrid(XElement listView, XNamespace presentationNamespace)
    {
        var rowTemplateGrid = listView
            .Element(presentationNamespace + "ListView.ItemTemplate")?
            .Element(presentationNamespace + "DataTemplate")?
            .Elements(presentationNamespace + "Grid")
            .FirstOrDefault();
        Assert.IsNotNull(rowTemplateGrid, "Expected LstContainers to define an item template root element.");
        return rowTemplateGrid!;
    }

    private static XElement FindMetadataGrid(XElement outerGrid)
    {
        var metadataGrid = outerGrid
            .Elements()
            .SingleOrDefault(element => (string?)element.Attribute("Grid.Column") == "0");
        Assert.IsNotNull(metadataGrid, "Expected the outer table grid to contain a metadata grid in Grid.Column=0.");
        return metadataGrid!;
    }

    private static XElement FindActionHost(XElement outerGrid)
    {
        var actionHost = outerGrid
            .Elements()
            .SingleOrDefault(element => (string?)element.Attribute("Grid.Column") == "1");
        Assert.IsNotNull(actionHost, "Expected the row template to define an action host in the outer Grid.Column=1 slot.");
        return actionHost!;
    }

    private static XElement FindButton(XElement container, XNamespace presentationNamespace, XNamespace xamlNamespace, string uid)
    {
        var button = container
            .Descendants(presentationNamespace + "Button")
            .SingleOrDefault(element => (string?)element.Attribute(xamlNamespace + "Uid") == uid);
        Assert.IsNotNull(button, $"Expected the action host to contain a Button with x:Uid='{uid}'.");
        return button!;
    }

    private static void AssertMetadataGridContract(XElement metadataGrid, XNamespace presentationNamespace, XNamespace tablesNamespace, string roleValue, int expectedSplitterCount)
    {
        Assert.AreEqual(
            roleValue,
            (string?)metadataGrid.Attribute(tablesNamespace + "TableLayoutBehavior.Role"),
            $"Expected the metadata grid role to be '{roleValue}'.");
        Assert.AreEqual(
            "True",
            (string?)metadataGrid.Attribute(tablesNamespace + "ClipToBoundsBehavior.IsEnabled"),
            "Expected the metadata grid to enable the table clip-to-bounds behavior.");
        Assert.AreEqual(
            "{StaticResource ContainersTableColumnLayout}",
            (string?)metadataGrid.Attribute(tablesNamespace + "TableLayoutBehavior.Layout"),
            "Expected the metadata grid to use the ContainersTableColumnLayout resource.");

        var columnDefinitions = metadataGrid
            .Element(presentationNamespace + "Grid.ColumnDefinitions")?
            .Elements(presentationNamespace + "ColumnDefinition")
            .ToList();
        Assert.IsNotNull(columnDefinitions, "Expected the metadata grid to define table columns.");
        Assert.AreEqual(7, columnDefinitions!.Count, "Expected the metadata grid to define 4 logical columns and 3 splitters.");

        var logicalColumns = columnDefinitions.Where((_, index) => index % 2 == 0).ToList();
        Assert.AreEqual(4, logicalColumns.Count, "Expected the metadata grid to define four logical columns.");
        CollectionAssert.AreEqual(
            new[] { "0", "1", "2", "3" },
            logicalColumns.Select(column => (string?)column.Attribute(tablesNamespace + "TableLayoutBehavior.ColumnIndex")).ToArray(),
            "Expected the logical columns to be tagged with column indexes 0 through 3.");

        var splitterColumns = columnDefinitions.Where((_, index) => index % 2 == 1).ToList();
        Assert.AreEqual(3, splitterColumns.Count, "Expected the metadata grid to define three splitter columns.");
        Assert.IsTrue(
            splitterColumns.All(column => (string?)column.Attribute("Width") == "8"),
            "Expected the nonlogical splitter columns to have fixed widths of 8.");

        var splitters = metadataGrid.Elements(tablesNamespace + "TableColumnSplitter").ToList();
        Assert.AreEqual(expectedSplitterCount, splitters.Count, "Expected the metadata grid to expose the reviewer-required number of table splitters.");
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
