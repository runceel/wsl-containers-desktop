using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WslContainersDesktop_App_Tests.Pages;

[TestClass]
public sealed class VolumesPageSourceTests
{
    private const string VolumesPageXamlPath = @"src\WslContainersDesktop.App\Pages\VolumesPage.xaml";
    private const string VolumesResourcesPath = @"src\WslContainersDesktop.App\Strings\en-US\Resources.resw";
    private const string TablesNamespaceName = "using:WslContainersDesktop_App.Tables";

    private static readonly XNamespace PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace TablesNamespace = TablesNamespaceName;

    [TestMethod]
    public void VolumesPage_XamlDisplaysRequiredVolumeFields()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(VolumesPageXamlPath);

        // Assert
        StringAssert.Contains(sourceText, "Text=\"{x:Bind Name}\"");
        StringAssert.Contains(sourceText, "Text=\"{x:Bind Driver}\"");
        StringAssert.Contains(sourceText, "Text=\"{x:Bind CreatedAt}\"");
        StringAssert.Contains(sourceText, "Text=\"{x:Bind UsageText}\"");
    }

    [TestMethod]
    public void VolumesPage_HeaderIncludesAccentBar()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(VolumesPageXamlPath);

        // Assert
        StringAssert.Contains(sourceText, "<ColumnDefinition Width=\"4\" />");
        StringAssert.Contains(sourceText, "Background=\"{ThemeResource WslContainersAccentFillColorDefaultBrush}\"");
        StringAssert.Contains(sourceText, "CornerRadius=\"{StaticResource ControlCornerRadius}\"");
    }

    [TestMethod]
    public void VolumesPage_ListViewItemsUseSharedTableStyle()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(VolumesPageXamlPath);
        var document = XDocument.Parse(sourceText);

        // Act
        var listView = FindVolumesListView(document);
        var rowTemplateGrid = FindRowTemplateGrid(listView);

        // Assert
        Assert.AreEqual(
            "{StaticResource TableListViewItemStyle}",
            (string?)listView.Attribute("ItemContainerStyle"),
            "The Volumes page should use the shared table list view item style.");
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
    public void VolumesPage_SourceUsesReviewedHeaderAndRowTableLayoutContract()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(VolumesPageXamlPath);
        var document = XDocument.Parse(sourceText);
        var tablesNamespace = document.Root?.GetNamespaceOfPrefix("tables");

        // Assert
        Assert.IsNotNull(tablesNamespace, "Expected the Volumes page XAML to declare the tables namespace.");
        Assert.AreEqual(
            TablesNamespaceName,
            tablesNamespace!.NamespaceName,
            "The Volumes page XAML should declare the tables namespace for table layout helpers.");
        Assert.IsFalse(sourceText.Contains("CommunityToolkit", StringComparison.OrdinalIgnoreCase), "The Volumes page should not reference CommunityToolkit concrete splitter types.");

        var listView = FindVolumesListView(document);
        var headerGrid = FindHeaderGrid(listView);
        var rowTemplateGrid = FindRowTemplateGrid(listView);

        AssertOuterGridContract(headerGrid);
        AssertOuterGridContract(rowTemplateGrid);

        var headerMetadataGrid = FindMetadataGrid(headerGrid);
        var rowMetadataGrid = FindMetadataGrid(rowTemplateGrid);
        AssertMetadataGridContract(headerMetadataGrid, expectedRole: "Header", expectedSplitterCount: 3);
        AssertMetadataGridContract(rowMetadataGrid, expectedRole: "Row", expectedSplitterCount: 0);
        AssertHeaderSplitterContract(headerMetadataGrid);
        AssertDeleteButtonContract(rowTemplateGrid);
    }

    [TestMethod]
    public void VolumesPage_Resources_UseLocalizedUidKeysForSplitters()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(VolumesResourcesPath);
        var document = XDocument.Parse(sourceText);

        // Assert
        var expectedLocalizedKeys = new[]
        {
            "SpltVolumesNameDriver.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name",
            "SpltVolumesDriverCreated.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name",
            "SpltVolumesCreatedUsage.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name"
        };

        foreach (var expectedLocalizedKey in expectedLocalizedKeys)
        {
            var entry = FindReswDataEntry(document, expectedLocalizedKey);
            Assert.IsNotNull(entry, $"Expected the resources to contain '{expectedLocalizedKey}'.");
            var value = entry!.Element("value")?.Value;
            Assert.IsFalse(string.IsNullOrWhiteSpace(value), $"Expected '{expectedLocalizedKey}' to have a nonempty localized value.");
        }
    }

    [TestMethod]
    public void VolumesPage_DeleteClickShowsConfirmationBeforeExecutingDeleteCommand()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\VolumesPage.xaml.cs");

        // Act
        var handlerIndex = sourceText.IndexOf("BtnDeleteVolume_Click", StringComparison.Ordinal);
        var dialogIndex = sourceText.IndexOf("ContentDialog", StringComparison.Ordinal);
        var primaryResultIndex = sourceText.IndexOf("ContentDialogResult.Primary", StringComparison.Ordinal);
        var deleteCommandIndex = sourceText.IndexOf("DeleteCommand.ExecuteAsync", StringComparison.Ordinal);

        // Assert
        Assert.IsGreaterThanOrEqualTo(0, handlerIndex, "Expected VolumesPage to define BtnDeleteVolume_Click.");
        Assert.IsGreaterThanOrEqualTo(0, dialogIndex, "Expected volume deletion to create a confirmation dialog.");
        Assert.IsGreaterThanOrEqualTo(0, primaryResultIndex, "Expected volume deletion to require primary confirmation.");
        Assert.IsGreaterThanOrEqualTo(0, deleteCommandIndex, "Expected confirmed volume deletion to execute DeleteCommand.");
        Assert.IsTrue(
            handlerIndex < dialogIndex && dialogIndex < deleteCommandIndex,
            "Expected the delete confirmation dialog to be created before executing the delete command.");
    }

    [TestMethod]
    public void VolumesPage_CreateProgressRing_IsBottomAligned()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(VolumesPageXamlPath);
        var ringPattern = new Regex(
            """
            <ProgressRing\b(?=[^>]*?x:Name="PrgCreateVolume")(?=[^>]*?VerticalAlignment="Bottom")[^>]*?/>
            """);

        // Assert
        Assert.IsTrue(
            ringPattern.IsMatch(sourceText),
            "Expected the PrgCreateVolume progress ring to be bottom-aligned so it lines up with the create button.");
    }

    [TestMethod]
    public void MainWindow_SourceContainsVolumesNavigationItemAndMapping()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\MainWindow.xaml");
        var codeBehindText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\MainWindow.xaml.cs");

        // Assert
        StringAssert.Contains(sourceText, "AutomationProperties.AutomationId=\"NavItemVolumes\"");
        StringAssert.Contains(codeBehindText, "[NavItemVolumes] = NavigationPageKey.Volumes");
    }

    private static XElement? FindReswDataEntry(XDocument document, string name)
    {
        return document
            .Descendants("data")
            .SingleOrDefault(element => (string?)element.Attribute("name") == name);
    }

    private static XElement FindVolumesListView(XDocument document)
    {
        var listView = document
            .Descendants(PresentationNamespace + "ListView")
            .SingleOrDefault(element => (string?)element.Attribute(XamlNamespace + "Name") == "LstVolumes");
        Assert.IsNotNull(listView, "Expected VolumesPage to define the LstVolumes ListView.");
        return listView!;
    }

    private static XElement FindHeaderGrid(XElement listView)
    {
        var headerGrid = listView.ElementsBeforeSelf().LastOrDefault();
        Assert.IsNotNull(headerGrid, "Expected LstVolumes to have a preceding header element.");
        Assert.AreEqual(
            PresentationNamespace + "Grid",
            headerGrid!.Name,
            "Expected LstVolumes' immediately preceding sibling to be the column-header Grid.");
        return headerGrid;
    }

    private static XElement FindRowTemplateGrid(XElement listView)
    {
        var rowTemplateGrid = listView
            .Element(PresentationNamespace + "ListView.ItemTemplate")?
            .Element(PresentationNamespace + "DataTemplate")?
            .Elements(PresentationNamespace + "Grid")
            .FirstOrDefault();
        Assert.IsNotNull(rowTemplateGrid, "Expected LstVolumes to define an item template root element.");
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
            "The outer table grid should use the reviewed table padding values.");
        Assert.AreEqual(
            "12",
            (string?)outerGrid.Attribute("ColumnSpacing"),
            "The outer table grid should use the reviewed table column spacing.");
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
        CollectionAssert.AreEqual(new[] { "*", "120" }, columnDefinitions!, "The outer table grid should use the reviewed column widths.");
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
            "{StaticResource VolumesTableColumnLayout}",
            (string?)metadataGrid.Attribute(TablesNamespace + "TableLayoutBehavior.Layout"),
            "Expected the metadata grid to use the VolumesTableColumnLayout resource.");

        var columnDefinitions = metadataGrid
            .Element(PresentationNamespace + "Grid.ColumnDefinitions")?
            .Elements(PresentationNamespace + "ColumnDefinition")
            .ToList();
        Assert.IsNotNull(columnDefinitions, "Expected the metadata grid to define table columns.");
        Assert.HasCount(7, columnDefinitions!, "Expected the metadata grid to define 4 logical columns and 3 splitters.");

        var logicalColumns = columnDefinitions.Where((_, index) => index % 2 == 0).ToList();
        Assert.HasCount(4, logicalColumns, "Expected the metadata grid to define four logical columns.");
        CollectionAssert.AreEqual(
            new[] { "0", "1", "2", "3" },
            logicalColumns.Select(column => (string?)column.Attribute(TablesNamespace + "TableLayoutBehavior.ColumnIndex")).ToArray(),
            "Expected the logical columns to be tagged with column indexes 0 through 3.");

        var splitterColumns = columnDefinitions.Where((_, index) => index % 2 == 1).ToList();
        Assert.HasCount(3, splitterColumns, "Expected the metadata grid to define three splitter columns.");
        Assert.IsTrue(
            splitterColumns.All(column => (string?)column.Attribute("Width") == "8"),
            "Expected the nonlogical splitter columns to have fixed widths of 8.");

        var splitters = metadataGrid.Elements(TablesNamespace + "TableColumnSplitter").ToList();
        Assert.HasCount(expectedSplitterCount, splitters, "Expected the metadata grid to expose the reviewer-required number of table splitters.");
    }

    private static void AssertHeaderSplitterContract(XElement headerMetadataGrid)
    {
        var headerSplitters = headerMetadataGrid.Elements(TablesNamespace + "TableColumnSplitter").ToList();
        Assert.HasCount(3, headerSplitters, "The header metadata grid should expose three table column splitters at the boundaries.");

        var expectedSplitters = new[]
        {
            (Uid: "SpltVolumesNameDriver", Column: "1"),
            (Uid: "SpltVolumesDriverCreated", Column: "3"),
            (Uid: "SpltVolumesCreatedUsage", Column: "5")
        };

        foreach (var (expectedUid, expectedColumn) in expectedSplitters)
        {
            var splitter = headerSplitters.SingleOrDefault(element => (string?)element.Attribute(XamlNamespace + "Uid") == expectedUid);
            Assert.IsNotNull(splitter, $"Expected header splitter '{expectedUid}' to exist.");
            Assert.AreEqual(
                expectedUid,
                (string?)splitter!.Attribute(XamlNamespace + "Uid"),
                $"Expected splitter '{expectedUid}' to use the reviewed x:Uid value.");
            Assert.AreEqual(
                expectedColumn,
                (string?)splitter.Attribute("Grid.Column"),
                $"Expected splitter '{expectedUid}' to appear in the reviewed column position.");
            Assert.AreEqual(
                expectedUid,
                (string?)splitter.Attribute("AutomationProperties.AutomationId"),
                $"Expected splitter '{expectedUid}' to use the reviewed automation id.");
        }
    }

    private static void AssertDeleteButtonContract(XElement rowTemplateGrid)
    {
        var actionHost = FindActionHost(rowTemplateGrid);
        var deleteButton = FindButtonByUid(actionHost, "BtnDeleteVolume");

        Assert.AreEqual(
            "BtnDeleteVolume_Click",
            (string?)deleteButton.Attribute("Click"),
            "The delete button should preserve the reviewed Click handler.");
        Assert.AreEqual(
            "{x:Bind}",
            (string?)deleteButton.Attribute("CommandParameter"),
            "The delete button should preserve the reviewed CommandParameter binding.");
        Assert.AreEqual(
            "{x:Bind CanDelete, Mode=OneWay}",
            (string?)deleteButton.Attribute("IsEnabled"),
            "The delete button should preserve the reviewed enabled binding.");
        Assert.AreEqual(
            "{x:Bind Name, Mode=OneWay, Converter={StaticResource AutomationIdConverter}, ConverterParameter=BtnDeleteVolume}",
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
