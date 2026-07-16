using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WslContainersDesktop_App_Tests.Pages;

[TestClass]
public sealed class NetworksPageSourceTests
{
    private const string NetworksPageXamlPath = @"src\WslContainersDesktop.App\Pages\NetworksPage.xaml";
    private const string NetworksResourcesPath = @"src\WslContainersDesktop.App\Strings\en-US\Resources.resw";
    private const string SplitterAutomationNameResourceSuffix = ".[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name";
    private const string TablesNamespaceName = "using:WslContainersDesktop_App.Tables";

    private static readonly XNamespace PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace TablesNamespace = TablesNamespaceName;
    private static readonly (string Uid, string Column)[] NetworkSplitterContracts =
    {
        ("SpltNetworksNameDriver", "1"),
        ("SpltNetworksDriverCreated", "3"),
        ("SpltNetworksCreatedContainers", "5"),
        ("SpltNetworksContainersType", "7"),
        ("SpltNetworksTypeUsage", "9")
    };

    [TestMethod]
    public void NetworksPage_XamlDisplaysRequiredNetworkFields()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(NetworksPageXamlPath);

        // Assert
        StringAssert.Contains(sourceText, "Text=\"{x:Bind Name}\"");
        StringAssert.Contains(sourceText, "Text=\"{x:Bind Driver}\"");
        StringAssert.Contains(sourceText, "Text=\"{x:Bind CreatedAtText}\"");
        StringAssert.Contains(sourceText, "Text=\"{x:Bind ConnectedContainerCountText}\"");
        StringAssert.Contains(sourceText, "Text=\"{x:Bind TypeText}\"");
    }

    [TestMethod]
    public void NetworksPage_HeaderIncludesAccentBar()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(NetworksPageXamlPath);

        // Assert
        StringAssert.Contains(sourceText, "<ColumnDefinition Width=\"4\" />");
        StringAssert.Contains(sourceText, "Background=\"{ThemeResource WslContainersAccentFillColorDefaultBrush}\"");
        StringAssert.Contains(sourceText, "CornerRadius=\"{StaticResource ControlCornerRadius}\"");
    }

    [TestMethod]
    public void NetworksPage_HeaderAndRows_UseResizableSixColumnLayoutWithFixedActionRail()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(NetworksPageXamlPath);
        var document = XDocument.Parse(sourceText);
        var tablesNamespace = document.Root?.GetNamespaceOfPrefix("tables");

        // Assert
        Assert.IsNotNull(tablesNamespace, "Expected the Networks page XAML to declare the tables namespace.");
        Assert.AreEqual(
            TablesNamespaceName,
            tablesNamespace!.NamespaceName,
            "The Networks page XAML should declare the tables namespace for table layout helpers.");
        Assert.IsFalse(sourceText.Contains("CommunityToolkit", StringComparison.OrdinalIgnoreCase), "The Networks page should not reference CommunityToolkit concrete splitter types.");

        var listView = FindNetworksListView(document);
        var headerGrid = FindHeaderGrid(listView);
        var rowTemplateGrid = FindRowTemplateGrid(listView);

        AssertOuterGridContract(headerGrid);
        AssertOuterGridContract(rowTemplateGrid);

        var headerMetadataGrid = FindMetadataGrid(headerGrid);
        var rowMetadataGrid = FindMetadataGrid(rowTemplateGrid);
        AssertMetadataGridContract(headerMetadataGrid, expectedRole: "Header", expectedSplitterCount: 5);
        AssertMetadataGridContract(rowMetadataGrid, expectedRole: "Row", expectedSplitterCount: 0);
        AssertHeaderSplitterContract(headerMetadataGrid);
        AssertDeleteButtonContract(rowTemplateGrid);
    }

    [TestMethod]
    public void NetworksPage_Resources_UseLocalizedUidKeysForSplitters()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(NetworksResourcesPath);
        var document = XDocument.Parse(sourceText);

        // Assert
        foreach (var (splitterUid, _) in NetworkSplitterContracts)
        {
            var expectedLocalizedKey = splitterUid + SplitterAutomationNameResourceSuffix;
            var entry = FindReswDataEntry(document, expectedLocalizedKey);
            Assert.IsNotNull(entry, $"Expected the resources to contain '{expectedLocalizedKey}'.");
            var value = entry!.Element("value")?.Value;
            Assert.IsFalse(string.IsNullOrWhiteSpace(value), $"Expected '{expectedLocalizedKey}' to have a nonempty localized value.");
        }
    }

    [TestMethod]
    public void NetworksPage_CreateProgressRing_IsBottomAligned()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(NetworksPageXamlPath);
        var ringPattern = new Regex(
            """
            <ProgressRing\b(?=[^>]*?x:Name="PrgCreateNetwork")(?=[^>]*?VerticalAlignment="Bottom")[^>]*?/>
            """);

        // Assert
        Assert.IsTrue(
            ringPattern.IsMatch(sourceText),
            "Expected the PrgCreateNetwork progress ring to be bottom-aligned so it lines up with the create button.");
    }

    [TestMethod]
    public void NetworksPage_NoNetworks_EmptyStateUsesDistinguishableSurfaceBorder()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(NetworksPageXamlPath);
        var emptyStateBorderPattern = new Regex(
            """
            <Border\b[^>]*x:Name="BorderNetworksEmptyState"[^>]*Background="\{ThemeResource LayerFillColorDefaultBrush\}"[^>]*BorderBrush="\{ThemeResource CardStrokeColorDefaultBrush\}"[^>]*BorderThickness="\{ThemeResource WslContainersSurfaceBorderThickness\}"[^>]*CornerRadius="\{StaticResource OverlayCornerRadius\}"[^>]*\sVisibility="\{x:Bind ToVisibleWhenFalse\(ViewModel\.HasNetworks\), Mode=OneWay\}"[^>]*>(?s:.*?)<TextBlock\b[^>]*x:Name="TxtNetworksEmptyState"
            """,
            RegexOptions.Singleline);

        // Assert
        Assert.IsTrue(
            emptyStateBorderPattern.IsMatch(sourceText),
            "Expected the empty-state section to be a named Border with a distinguishable layer surface, a card stroke, and the visible-when-false binding.");
    }

    [TestMethod]
    public void NetworksPage_DeleteClickShowsConfirmationBeforeExecutingDeleteCommand()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\NetworksPage.xaml.cs");

        // Act
        var handlerIndex = sourceText.IndexOf("BtnDeleteNetwork_Click", StringComparison.Ordinal);
        var dialogIndex = sourceText.IndexOf("ContentDialog", StringComparison.Ordinal);
        var primaryResultIndex = sourceText.IndexOf("ContentDialogResult.Primary", StringComparison.Ordinal);
        var deleteCommandIndex = sourceText.IndexOf("DeleteCommand.ExecuteAsync", StringComparison.Ordinal);

        // Assert
        Assert.IsGreaterThanOrEqualTo(0, handlerIndex, "Expected NetworksPage to define BtnDeleteNetwork_Click.");
        Assert.IsGreaterThanOrEqualTo(0, dialogIndex, "Expected network deletion to create a confirmation dialog.");
        Assert.IsGreaterThanOrEqualTo(0, primaryResultIndex, "Expected network deletion to require primary confirmation.");
        Assert.IsGreaterThanOrEqualTo(0, deleteCommandIndex, "Expected confirmed network deletion to execute DeleteCommand.");
        Assert.IsTrue(
            handlerIndex < dialogIndex && dialogIndex < deleteCommandIndex,
            "Expected the delete confirmation dialog to be created before executing the delete command.");
    }

    [TestMethod]
    public void MainWindow_SourceContainsNetworksNavigationItemAndMapping()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\MainWindow.xaml");
        var codeBehindText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\MainWindow.xaml.cs");

        // Assert
        StringAssert.Contains(sourceText, "AutomationProperties.AutomationId=\"NavItemNetworks\"");
        StringAssert.Contains(codeBehindText, "[NavItemNetworks] = NavigationPageKey.Networks");
    }

    private static XElement? FindReswDataEntry(XDocument document, string name)
    {
        return document
            .Descendants("data")
            .SingleOrDefault(element => (string?)element.Attribute("name") == name);
    }

    private static XElement FindNetworksListView(XDocument document)
    {
        var listView = document
            .Descendants(PresentationNamespace + "ListView")
            .SingleOrDefault(element => (string?)element.Attribute(XamlNamespace + "Name") == "LstNetworks");
        Assert.IsNotNull(listView, "Expected NetworksPage to define the LstNetworks ListView.");
        return listView!;
    }

    private static XElement FindHeaderGrid(XElement listView)
    {
        var headerGrid = listView.ElementsBeforeSelf().LastOrDefault();
        Assert.IsNotNull(headerGrid, "Expected LstNetworks to have a preceding header element.");
        Assert.AreEqual(
            PresentationNamespace + "Grid",
            headerGrid!.Name,
            "Expected LstNetworks' immediately preceding sibling to be the column-header Grid.");
        return headerGrid;
    }

    private static XElement FindRowTemplateGrid(XElement listView)
    {
        var rowTemplateGrid = listView
            .Element(PresentationNamespace + "ListView.ItemTemplate")?
            .Element(PresentationNamespace + "DataTemplate")?
            .Elements(PresentationNamespace + "Grid")
            .FirstOrDefault();
        Assert.IsNotNull(rowTemplateGrid, "Expected LstNetworks to define an item template root element.");
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

    private static void AssertOuterGridContract(XElement outerGrid)
    {
        Assert.AreEqual(
            "12,8",
            (string?)outerGrid.Attribute("Padding"),
            "The outer table grid should use 12,8 padding.");
        Assert.AreEqual(
            "12",
            (string?)outerGrid.Attribute("ColumnSpacing"),
            "The outer table grid should use 12 pixels of column spacing.");
        Assert.AreEqual(
            "Stretch",
            (string?)outerGrid.Attribute("HorizontalAlignment"),
            "The outer table grid should stretch horizontally to fill the table layout.");

        var columnDefinitions = outerGrid
            .Element(PresentationNamespace + "Grid.ColumnDefinitions")?
            .Elements(PresentationNamespace + "ColumnDefinition")
            .Select(element => (string?)element.Attribute("Width"))
            .ToArray();
        Assert.IsNotNull(columnDefinitions, "Expected the outer table grid to define column widths.");
        CollectionAssert.AreEqual(new[] { "*", "120" }, columnDefinitions!, "The outer table grid should reserve a flexible data region and a fixed 120-pixel action rail.");
    }

    private static void AssertMetadataGridContract(XElement metadataGrid, string expectedRole, int expectedSplitterCount)
    {
        Assert.AreEqual(
            expectedRole,
            (string?)metadataGrid.Attribute(TablesNamespace + "TableLayoutBehavior.Role"),
            $"Expected the metadata grid role to be '{expectedRole}'.");
        Assert.AreEqual(
            "True",
            (string?)metadataGrid.Attribute(TablesNamespace + "ClipToBoundsBehavior.IsEnabled"),
            "Expected the metadata grid to enable the table clip-to-bounds behavior.");
        Assert.AreEqual(
            "{StaticResource NetworksTableColumnLayout}",
            (string?)metadataGrid.Attribute(TablesNamespace + "TableLayoutBehavior.Layout"),
            "Expected the metadata grid to use the NetworksTableColumnLayout resource.");

        var columnDefinitions = metadataGrid
            .Element(PresentationNamespace + "Grid.ColumnDefinitions")?
            .Elements(PresentationNamespace + "ColumnDefinition")
            .ToList();
        Assert.IsNotNull(columnDefinitions, "Expected the metadata grid to define table columns.");
        Assert.HasCount(11, columnDefinitions!, "Expected the metadata grid to define 6 logical columns and 5 splitter columns.");

        var logicalColumns = columnDefinitions.Where((_, index) => index % 2 == 0).ToList();
        Assert.HasCount(6, logicalColumns, "Expected the metadata grid to define six logical columns.");
        CollectionAssert.AreEqual(
            new[] { "0", "1", "2", "3", "4", "5" },
            logicalColumns.Select(column => (string?)column.Attribute(TablesNamespace + "TableLayoutBehavior.ColumnIndex")).ToArray(),
            "Expected the logical columns to be tagged with column indexes 0 through 5.");

        var splitterColumns = columnDefinitions.Where((_, index) => index % 2 == 1).ToList();
        Assert.HasCount(5, splitterColumns, "Expected the metadata grid to define five splitter columns.");
        Assert.IsTrue(
            splitterColumns.All(column => (string?)column.Attribute("Width") == "8"),
            "Expected the nonlogical splitter columns to have fixed widths of 8.");

        var splitters = metadataGrid.Elements(TablesNamespace + "TableColumnSplitter").ToList();
        Assert.HasCount(expectedSplitterCount, splitters, $"Expected the {expectedRole} grid to expose {expectedSplitterCount} table splitters.");
    }

    private static void AssertHeaderSplitterContract(XElement headerMetadataGrid)
    {
        var headerSplitters = headerMetadataGrid.Elements(TablesNamespace + "TableColumnSplitter").ToList();
        Assert.HasCount(NetworkSplitterContracts.Length, headerSplitters, "The header metadata grid should expose five table column splitters at the boundaries.");

        foreach (var (expectedUid, expectedColumn) in NetworkSplitterContracts)
        {
            var splitter = headerSplitters.SingleOrDefault(element => (string?)element.Attribute(XamlNamespace + "Uid") == expectedUid);
            Assert.IsNotNull(splitter, $"Expected header splitter '{expectedUid}' to exist.");
            Assert.AreEqual(
                expectedColumn,
                (string?)splitter!.Attribute("Grid.Column"),
                $"Expected splitter '{expectedUid}' to appear in column {expectedColumn}.");
            Assert.AreEqual(
                expectedUid,
                (string?)splitter.Attribute("AutomationProperties.AutomationId"),
                $"Expected splitter '{expectedUid}' to use its UID as its automation id.");
        }
    }

    private static void AssertDeleteButtonContract(XElement rowTemplateGrid)
    {
        var actionHost = FindActionHost(rowTemplateGrid);
        var deleteButton = FindButtonByUid(actionHost, "BtnDeleteNetwork");

        Assert.AreEqual(
            "BtnDeleteNetwork_Click",
            (string?)deleteButton.Attribute("Click"),
            "The delete button should invoke the network deletion confirmation handler.");
        Assert.AreEqual(
            "{x:Bind}",
            (string?)deleteButton.Attribute("CommandParameter"),
            "The delete button should pass the current network row to the confirmation handler.");
        Assert.AreEqual(
            "{x:Bind CanDelete, Mode=OneWay}",
            (string?)deleteButton.Attribute("IsEnabled"),
            "The delete button enabled state should track CanDelete.");
        Assert.AreEqual(
            "{x:Bind Name, Mode=OneWay, Converter={StaticResource AutomationIdConverter}, ConverterParameter=BtnDeleteNetwork}",
            (string?)deleteButton.Attribute("AutomationProperties.AutomationId"),
            "The delete button should preserve the row-specific automation id binding.");
    }

    private static XElement FindButtonByUid(XElement container, string uid)
    {
        var button = container
            .Descendants(PresentationNamespace + "Button")
            .SingleOrDefault(element => (string?)element.Attribute(XamlNamespace + "Uid") == uid);
        Assert.IsNotNull(button, $"Expected the action host to contain a Button with x:Uid='{uid}'.");
        return button!;
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
