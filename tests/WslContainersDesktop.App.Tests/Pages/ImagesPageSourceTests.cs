using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WslContainersDesktop_App_Tests.Pages;

[TestClass]
public sealed class ImagesPageSourceTests
{
    [TestMethod]
    public void ImagesPage_XamlDisplaysRequiredImageFields()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ImagesPage.xaml");

        // Act
        // No-op: the test validates source bindings required by the feature specification.

        // Assert
        StringAssert.Contains(sourceText, "Text=\"{x:Bind DisplayName}\"");
        StringAssert.Contains(sourceText, "Text=\"{x:Bind Id}\"");
        StringAssert.Contains(sourceText, "CreatedAt");
    }

    [TestMethod]
    public void ImagesPage_RowIncludesRunActionWithAutomationId()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ImagesPage.xaml");

        // Assert
        StringAssert.Contains(sourceText, "BtnRunImage");
        StringAssert.Contains(sourceText, "ConverterParameter=BtnRunImage");
    }

    [TestMethod]
    public void ImagesPage_RunDialogIncludesDetailedFields()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ImagesPage.xaml");

        // Assert
        StringAssert.Contains(sourceText, "TxtRunContainerName");
        StringAssert.Contains(sourceText, "TglRunRemoveWhenStopped");
        StringAssert.Contains(sourceText, "TxtRunPortMappings");
        StringAssert.Contains(sourceText, "TxtRunEnvironmentVariables");
        StringAssert.Contains(sourceText, "TxtRunCommand");
    }

    [TestMethod]
    public void ImagesPage_RunClickShowsDialogBeforeExecutingRunCommand()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ImagesPage.xaml.cs");

        // Act
        var handlerIndex = sourceText.IndexOf("BtnRunImage_Click", StringComparison.Ordinal);
        var handlerSearchStart = Math.Max(handlerIndex, 0);
        var dialogIndex = sourceText.IndexOf("RunImageDialog.ShowAsync", handlerSearchStart, StringComparison.Ordinal);
        var primaryResultIndex = sourceText.IndexOf("ContentDialogResult.Primary", handlerSearchStart, StringComparison.Ordinal);
        var runCommandIndex = sourceText.IndexOf("RunCommand.ExecuteAsync", handlerSearchStart, StringComparison.Ordinal);

        // Assert
        Assert.IsGreaterThanOrEqualTo(0, handlerIndex, "Expected ImagesPage to define BtnRunImage_Click.");
        Assert.IsGreaterThanOrEqualTo(0, dialogIndex, "Expected image run to show the run configuration dialog.");
        Assert.IsGreaterThanOrEqualTo(0, primaryResultIndex, "Expected image run to require primary confirmation.");
        Assert.IsGreaterThanOrEqualTo(0, runCommandIndex, "Expected confirmed image run to execute RunCommand.");
        Assert.IsTrue(
            handlerIndex < dialogIndex && dialogIndex < primaryResultIndex && primaryResultIndex < runCommandIndex,
            "Expected the run configuration dialog to be shown before executing the run command.");
    }

    [TestMethod]
    public void ImagesPage_HeaderIncludesAccentBar()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ImagesPage.xaml");

        // Assert
        StringAssert.Contains(sourceText, "<ColumnDefinition Width=\"4\" />");
        StringAssert.Contains(sourceText, "Background=\"{ThemeResource WslContainersAccentFillColorDefaultBrush}\"");
        StringAssert.Contains(sourceText, "CornerRadius=\"{StaticResource ControlCornerRadius}\"");
    }

    [TestMethod]
    public void ImagesPage_ListSectionIncludesColumnHeaders()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ImagesPage.xaml");

        // Assert
        StringAssert.Contains(sourceText, "x:Name=\"TxtImageColumnName\"");
        StringAssert.Contains(sourceText, "x:Name=\"TxtImageColumnId\"");
        StringAssert.Contains(sourceText, "x:Name=\"TxtImageColumnSize\"");
        StringAssert.Contains(sourceText, "x:Name=\"TxtImageColumnCreatedAt\"");
        StringAssert.Contains(sourceText, "Grid.Row=\"1\"");
        StringAssert.Contains(sourceText, "Background=\"{ThemeResource LayerFillColorDefaultBrush}\"");
    }

    [TestMethod]
    public void ImagesPage_ListViewItemsUseSharedStretchStyle()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ImagesPage.xaml");
        var document = XDocument.Parse(sourceText);
        XNamespace presentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

        // Act
        var listView = FindImagesListView(document, presentationNamespace, xamlNamespace);
        var rowTemplateGrid = FindRowTemplateGrid(listView, presentationNamespace);

        // Assert
        Assert.AreEqual(
            "{StaticResource TableListViewItemStyle}",
            (string?)listView.Attribute("ItemContainerStyle"),
            "The Images page should use the shared table list view item style.");
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
        Assert.IsTrue(
            rowTemplateGrid.Descendants(presentationNamespace + "TextBlock").Any(element => (string?)element.Attribute("Text") == "{x:Bind DisplayName}"),
            "The row template should preserve the shared DisplayName binding.");
        Assert.IsTrue(
            rowTemplateGrid.Descendants(presentationNamespace + "TextBlock").Any(element => (string?)element.Attribute("Text") == "{x:Bind Id}"),
            "The row template should preserve the shared Id binding.");
    }

    [TestMethod]
    public void ImagesPage_SourceUsesReviewedHeaderAndRowTableLayoutContract()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ImagesPage.xaml");
        var document = XDocument.Parse(sourceText);
        XNamespace presentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
        var tablesNamespace = document.Root?.GetNamespaceOfPrefix("tables");

        // Assert
        Assert.IsNotNull(tablesNamespace, "Expected the Images page XAML to declare the tables namespace.");
        Assert.AreEqual(
            "using:WslContainersDesktop_App.Tables",
            tablesNamespace!.NamespaceName,
            "The Images page XAML should declare the tables namespace for table layout helpers.");
        Assert.IsFalse(sourceText.Contains("CommunityToolkit", StringComparison.OrdinalIgnoreCase), "The Images page should not reference CommunityToolkit concrete splitter types.");

        var listView = FindImagesListView(document, presentationNamespace, xamlNamespace);
        var headerGrid = FindHeaderGrid(listView, presentationNamespace);
        var rowTemplateGrid = FindRowTemplateGrid(listView, presentationNamespace);

        CollectionAssert.AreEqual(
            new[] { "*", "224" },
            headerGrid
                .Element(presentationNamespace + "Grid.ColumnDefinitions")?
                .Elements(presentationNamespace + "ColumnDefinition")
                .Select(element => (string?)element.Attribute("Width"))
                .ToArray(),
            "The header outer grid should use the reviewed metadata/action column widths.");
        CollectionAssert.AreEqual(
            new[] { "*", "224" },
            rowTemplateGrid
                .Element(presentationNamespace + "Grid.ColumnDefinitions")?
                .Elements(presentationNamespace + "ColumnDefinition")
                .Select(element => (string?)element.Attribute("Width"))
                .ToArray(),
            "The row template outer grid should use the reviewed metadata/action column widths.");

        var headerMetadataGrid = FindMetadataGrid(headerGrid);
        var rowMetadataGrid = FindMetadataGrid(rowTemplateGrid);
        AssertMetadataGridContract(headerMetadataGrid, presentationNamespace, tablesNamespace, expectedRole: "Header", expectedSplitterCount: 3);
        AssertMetadataGridContract(rowMetadataGrid, presentationNamespace, tablesNamespace, expectedRole: "Row", expectedSplitterCount: 0);

        var headerSplitters = headerMetadataGrid.Elements(tablesNamespace + "TableColumnSplitter").ToList();
        Assert.AreEqual(3, headerSplitters.Count, "The header metadata grid should expose three table column splitters at the boundaries.");
        var expectedSplitterUids = new[] { "SpltImagesNameId", "SpltImagesIdSize", "SpltImagesSizeCreated" };
        foreach (var expectedSplitterUid in expectedSplitterUids)
        {
            var splitter = headerSplitters.SingleOrDefault(element => (string?)element.Attribute(xamlNamespace + "Uid") == expectedSplitterUid);
            Assert.IsNotNull(splitter, $"Expected header splitter '{expectedSplitterUid}' to exist.");
            Assert.AreEqual(
                expectedSplitterUid,
                (string?)splitter!.Attribute("AutomationProperties.AutomationId"),
                $"Expected splitter '{expectedSplitterUid}' to use the reviewed automation id.");
        }

        var actionHost = FindActionHost(rowTemplateGrid);
        AssertButtonExists(actionHost, presentationNamespace, xamlNamespace, "BtnRunImage");
        AssertButtonExists(actionHost, presentationNamespace, xamlNamespace, "BtnDeleteImage");
    }

    [TestMethod]
    public void ImagesPage_Resources_UseLocalizedUidKeysForSplitters()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Strings\en-US\Resources.resw");
        var document = XDocument.Parse(sourceText);

        // Assert
        var expectedLocalizedKeys = new[]
        {
            "SpltImagesNameId.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name",
            "SpltImagesIdSize.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name",
            "SpltImagesSizeCreated.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name"
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
    public void ImagesPage_PullProgressRing_IsBottomAligned()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ImagesPage.xaml");
        var ringPattern = new Regex(
            """
            <ProgressRing\b(?=[^>]*?x:Name="PrgPullImage")(?=[^>]*?VerticalAlignment="Bottom")[^>]*?/>
            """);

        // Assert
        Assert.IsTrue(
            ringPattern.IsMatch(sourceText),
            "Expected the PrgPullImage progress ring to be bottom-aligned so it lines up with the pull button.");
    }

    [TestMethod]
    public void ImagesPage_DeleteClickShowsConfirmationBeforeExecutingDeleteCommand()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ImagesPage.xaml.cs");

        // Act
        var handlerIndex = sourceText.IndexOf("BtnDeleteImage_Click", StringComparison.Ordinal);
        var dialogIndex = sourceText.IndexOf("ContentDialog", StringComparison.Ordinal);
        var primaryResultIndex = sourceText.IndexOf("ContentDialogResult.Primary", StringComparison.Ordinal);
        var deleteCommandIndex = sourceText.IndexOf("DeleteCommand.ExecuteAsync", StringComparison.Ordinal);

        // Assert
        Assert.IsGreaterThanOrEqualTo(0, handlerIndex, "Expected ImagesPage to define BtnDeleteImage_Click.");
        Assert.IsGreaterThanOrEqualTo(0, dialogIndex, "Expected image deletion to create a confirmation dialog.");
        Assert.IsGreaterThanOrEqualTo(0, primaryResultIndex, "Expected image deletion to require primary confirmation.");
        Assert.IsGreaterThanOrEqualTo(0, deleteCommandIndex, "Expected confirmed image deletion to execute DeleteCommand.");
        Assert.IsTrue(
            handlerIndex < dialogIndex && dialogIndex < deleteCommandIndex,
            "Expected the delete confirmation dialog to be created before executing the delete command.");
    }

    private static XElement? FindReswDataEntry(XDocument document, string name)
    {
        return document
            .Descendants("data")
            .SingleOrDefault(element => (string?)element.Attribute("name") == name);
    }

    private static XElement FindImagesListView(XDocument document, XNamespace presentationNamespace, XNamespace xamlNamespace)
    {
        var listView = document
            .Descendants(presentationNamespace + "ListView")
            .SingleOrDefault(element => (string?)element.Attribute(xamlNamespace + "Name") == "LstImages");
        Assert.IsNotNull(listView, "Expected ImagesPage to define the LstImages ListView.");
        return listView!;
    }

    private static XElement FindHeaderGrid(XElement listView, XNamespace presentationNamespace)
    {
        var headerGrid = listView.ElementsBeforeSelf().LastOrDefault();
        Assert.IsNotNull(headerGrid, "Expected LstImages to have a preceding header element.");
        Assert.AreEqual(
            presentationNamespace + "Grid",
            headerGrid!.Name,
            "Expected LstImages' immediately preceding sibling to be the column-header Grid.");
        return headerGrid;
    }

    private static XElement FindRowTemplateGrid(XElement listView, XNamespace presentationNamespace)
    {
        var rowTemplateGrid = listView
            .Element(presentationNamespace + "ListView.ItemTemplate")?
            .Element(presentationNamespace + "DataTemplate")?
            .Elements(presentationNamespace + "Grid")
            .FirstOrDefault();
        Assert.IsNotNull(rowTemplateGrid, "Expected LstImages to define an item template root element.");
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

    private static void AssertButtonExists(XElement container, XNamespace presentationNamespace, XNamespace xamlNamespace, string uid)
    {
        var button = container
            .Descendants(presentationNamespace + "Button")
            .SingleOrDefault(element => (string?)element.Attribute(xamlNamespace + "Uid") == uid);
        Assert.IsNotNull(button, $"Expected the action host to contain a Button with x:Uid='{uid}'.");
    }

    private static void AssertMetadataGridContract(XElement metadataGrid, XNamespace presentationNamespace, XNamespace tablesNamespace, string expectedRole, int expectedSplitterCount)
    {
        Assert.AreEqual(
            expectedRole,
            (string?)metadataGrid.Attribute(tablesNamespace + "TableLayoutBehavior.Role"),
            $"Expected the metadata grid role to be '{expectedRole}'.");
        Assert.AreEqual(
            "True",
            (string?)metadataGrid.Attribute(tablesNamespace + "ClipToBoundsBehavior.IsEnabled"),
            "Expected the metadata grid to enable the table clip-to-bounds behavior.");
        Assert.AreEqual(
            "{StaticResource ImagesTableColumnLayout}",
            (string?)metadataGrid.Attribute(tablesNamespace + "TableLayoutBehavior.Layout"),
            "Expected the metadata grid to use the ImagesTableColumnLayout resource.");

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
