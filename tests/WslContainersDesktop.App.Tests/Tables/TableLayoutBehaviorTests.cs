using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using WslContainersDesktop_App.Tables;

namespace WslContainersDesktop_App_Tests.Tables;

[TestClass]
[DoNotParallelize]
public class TableLayoutBehaviorTests
{
    [UITestMethod]
    public void Attach_Header_AppliesLayoutWidthAndConstraintsToMarkedColumns()
    {
        // Arrange
        var layout = CreateContainersLayout();
        var grid = CreateGridWithInterleavedColumns(layout, TableLayoutRole.Header);

        // Act
        TableLayoutBehavior.Attach(grid);

        // Assert
        AssertColumnMatchesLayout(grid.ColumnDefinitions[0], layout, 0);
        AssertColumnMatchesLayout(grid.ColumnDefinitions[2], layout, 1);
        AssertColumnMatchesLayout(grid.ColumnDefinitions[4], layout, 2);
        AssertColumnMatchesLayout(grid.ColumnDefinitions[6], layout, 3);
    }

    [UITestMethod]
    public void Attach_Header_PreservesUnmarkedSplitterColumns()
    {
        // Arrange
        var layout = CreateContainersLayout();
        var grid = CreateGridWithInterleavedColumns(layout, TableLayoutRole.Header);

        // Act
        TableLayoutBehavior.Attach(grid);

        // Assert
        AssertColumnRemainsUnchanged(grid.ColumnDefinitions[1], new GridLength(8d, GridUnitType.Pixel), 8d, 8d);
        AssertColumnRemainsUnchanged(grid.ColumnDefinitions[3], new GridLength(8d, GridUnitType.Pixel), 8d, 8d);
        AssertColumnRemainsUnchanged(grid.ColumnDefinitions[5], new GridLength(8d, GridUnitType.Pixel), 8d, 8d);
        AssertColumnRemainsUnchanged(grid.ColumnDefinitions[7], new GridLength(8d, GridUnitType.Pixel), 8d, 8d);
    }

    [UITestMethod]
    public void Attach_Row_AppliesLayoutWidthAndConstraintsToMarkedColumns()
    {
        // Arrange
        var layout = CreateContainersLayout();
        var grid = CreateGridWithLogicalColumns(layout, TableLayoutRole.Row);

        // Act
        TableLayoutBehavior.Attach(grid);

        // Assert
        AssertColumnMatchesLayout(grid.ColumnDefinitions[0], layout, 0);
        AssertColumnMatchesLayout(grid.ColumnDefinitions[1], layout, 1);
        AssertColumnMatchesLayout(grid.ColumnDefinitions[2], layout, 2);
        AssertColumnMatchesLayout(grid.ColumnDefinitions[3], layout, 3);
    }

    [UITestMethod]
    public void Apply_LayoutWidthChanged_ReappliesCurrentLayoutToGrid()
    {
        // Arrange
        var layout = CreateContainersLayout();
        var grid = CreateGridWithLogicalColumns(layout, TableLayoutRole.Header);
        TableLayoutBehavior.Attach(grid);

        // Act
        layout.SetWidth(1, new GridLength(240d, GridUnitType.Pixel));
        TableLayoutBehavior.Apply(grid);

        // Assert
        var column = grid.ColumnDefinitions[1];
        Assert.AreEqual(GridUnitType.Pixel, column.Width.GridUnitType);
        Assert.AreEqual(240d, column.Width.Value);
    }

    [UITestMethod]
    public void AttachedProperties_RoundTripLayoutRoleAndColumnIndex()
    {
        // Arrange
        var layout = CreateContainersLayout();
        var grid = new Grid();
        var column = new ColumnDefinition();

        // Act
        TableLayoutBehavior.SetLayout(grid, layout);
        TableLayoutBehavior.SetRole(grid, TableLayoutRole.Header);
        TableLayoutBehavior.SetColumnIndex(column, 3);

        // Assert
        Assert.AreSame(layout, TableLayoutBehavior.GetLayout(grid));
        Assert.AreEqual(TableLayoutRole.Header, TableLayoutBehavior.GetRole(grid));
        Assert.AreEqual(3, TableLayoutBehavior.GetColumnIndex(column));
    }

    [UITestMethod]
    public void Detach_BeforeAndAfterAttach_IsIdempotentAndPreservesAppliedWidths()
    {
        // Arrange
        var layout = CreateContainersLayout();
        var grid = CreateGridWithLogicalColumns(layout, TableLayoutRole.Header);
        var column = grid.ColumnDefinitions[1];

        // Act
        TableLayoutBehavior.Detach(grid);
        TableLayoutBehavior.Attach(grid);
        var appliedWidth = column.Width;
        var appliedMinWidth = column.MinWidth;
        var appliedMaxWidth = column.MaxWidth;
        TableLayoutBehavior.Detach(grid);
        TableLayoutBehavior.Detach(grid);

        // Assert
        Assert.AreEqual(appliedWidth, column.Width);
        Assert.AreEqual(appliedMinWidth, column.MinWidth);
        Assert.AreEqual(appliedMaxWidth, column.MaxWidth);
    }

    [UITestMethod]
    public void SetWidth_LayoutChangedAfterAttach_UpdatesHeaderAndExistingRow()
    {
        // Arrange
        var layout = CreateContainersLayout();
        var headerGrid = CreateGridWithInterleavedColumns(layout, TableLayoutRole.Header);
        var rowGrid = CreateGridWithLogicalColumns(layout, TableLayoutRole.Row);
        TableLayoutBehavior.Attach(headerGrid);
        TableLayoutBehavior.Attach(rowGrid);

        // Act
        layout.SetWidth(1, new GridLength(240d, GridUnitType.Pixel));

        // Assert
        AssertColumnWidthEquals(headerGrid.ColumnDefinitions[2], 240d);
        AssertColumnWidthEquals(rowGrid.ColumnDefinitions[1], 240d);
    }

    [UITestMethod]
    public void HeaderWidthChanged_UpdatesLayoutAndExistingRow()
    {
        // Arrange
        var layout = CreateContainersLayout();
        var headerGrid = CreateGridWithInterleavedColumns(layout, TableLayoutRole.Header);
        var rowGrid = CreateGridWithLogicalColumns(layout, TableLayoutRole.Row);
        TableLayoutBehavior.Attach(headerGrid);
        TableLayoutBehavior.Attach(rowGrid);

        // Act
        headerGrid.ColumnDefinitions[2].Width = new GridLength(260d, GridUnitType.Pixel);

        // Assert
        Assert.AreEqual(260d, layout.GetWidth(1).Value);
        AssertColumnWidthEquals(headerGrid.ColumnDefinitions[2], 260d);
        AssertColumnWidthEquals(rowGrid.ColumnDefinitions[1], 260d);
    }

    [UITestMethod]
    public void HeaderWidthChangedBelowMinimum_ClampsLayoutHeaderAndRow()
    {
        // Arrange
        var layout = CreateContainersLayout();
        var headerGrid = CreateGridWithInterleavedColumns(layout, TableLayoutRole.Header);
        var rowGrid = CreateGridWithLogicalColumns(layout, TableLayoutRole.Row);
        TableLayoutBehavior.Attach(headerGrid);
        TableLayoutBehavior.Attach(rowGrid);

        // Act
        headerGrid.ColumnDefinitions[2].Width = new GridLength(10d, GridUnitType.Pixel);

        // Assert
        Assert.AreEqual(100d, layout.GetWidth(1).Value);
        AssertColumnWidthEquals(headerGrid.ColumnDefinitions[2], 100d);
        AssertColumnWidthEquals(rowGrid.ColumnDefinitions[1], 100d);
    }

    [UITestMethod]
    public void Attach_RowAfterLayoutChanged_AppliesLatestWidth()
    {
        // Arrange
        var layout = CreateContainersLayout();
        var headerGrid = CreateGridWithInterleavedColumns(layout, TableLayoutRole.Header);
        TableLayoutBehavior.Attach(headerGrid);
        headerGrid.ColumnDefinitions[2].Width = new GridLength(250d, GridUnitType.Pixel);

        // Act
        var rowGrid = CreateGridWithLogicalColumns(layout, TableLayoutRole.Row);
        TableLayoutBehavior.Attach(rowGrid);

        // Assert
        Assert.AreEqual(250d, layout.GetWidth(1).Value);
        AssertColumnWidthEquals(rowGrid.ColumnDefinitions[1], 250d);
    }

    [UITestMethod]
    public void Detach_RowThenLayoutChanges_StopsUpdates_AndReattachAppliesLatest()
    {
        // Arrange
        var layout = CreateContainersLayout();
        var headerGrid = CreateGridWithInterleavedColumns(layout, TableLayoutRole.Header);
        var rowGrid = CreateGridWithLogicalColumns(layout, TableLayoutRole.Row);
        TableLayoutBehavior.Attach(headerGrid);
        TableLayoutBehavior.Attach(rowGrid);

        // Act
        TableLayoutBehavior.Detach(rowGrid);
        layout.SetWidth(1, new GridLength(270d, GridUnitType.Pixel));

        // Assert
        AssertColumnWidthEquals(rowGrid.ColumnDefinitions[1], 130d);

        // Act
        TableLayoutBehavior.Attach(rowGrid);

        // Assert
        Assert.AreEqual(270d, layout.GetWidth(1).Value);
        AssertColumnWidthEquals(rowGrid.ColumnDefinitions[1], 270d);
    }

    [UITestMethod]
    public void Detach_HeaderThenHeaderChanges_DoesNotUpdateLayoutOrRow()
    {
        // Arrange
        var layout = CreateContainersLayout();
        var headerGrid = CreateGridWithInterleavedColumns(layout, TableLayoutRole.Header);
        var rowGrid = CreateGridWithLogicalColumns(layout, TableLayoutRole.Row);
        TableLayoutBehavior.Attach(headerGrid);
        TableLayoutBehavior.Attach(rowGrid);

        // Act
        TableLayoutBehavior.Detach(headerGrid);
        headerGrid.ColumnDefinitions[2].Width = new GridLength(280d, GridUnitType.Pixel);

        // Assert
        Assert.AreEqual(130d, layout.GetWidth(1).Value);
        AssertColumnWidthEquals(rowGrid.ColumnDefinitions[1], 130d);
    }

    [UITestMethod]
    public void RowWidthChanged_DoesNotUpdateLayoutOrOtherRows()
    {
        // Arrange
        var layout = CreateContainersLayout();
        var headerGrid = CreateGridWithInterleavedColumns(layout, TableLayoutRole.Header);
        var firstRowGrid = CreateGridWithLogicalColumns(layout, TableLayoutRole.Row);
        var secondRowGrid = CreateGridWithLogicalColumns(layout, TableLayoutRole.Row);
        TableLayoutBehavior.Attach(headerGrid);
        TableLayoutBehavior.Attach(firstRowGrid);
        TableLayoutBehavior.Attach(secondRowGrid);

        // Act
        firstRowGrid.ColumnDefinitions[1].Width = new GridLength(280d, GridUnitType.Pixel);

        // Assert
        Assert.AreEqual(130d, layout.GetWidth(1).Value);
        AssertColumnWidthEquals(firstRowGrid.ColumnDefinitions[1], 280d);
        AssertColumnWidthEquals(secondRowGrid.ColumnDefinitions[1], 130d);
    }

    [UITestMethod]
    public void HeaderWidthChanged_NotifiesLayoutOnce()
    {
        // Arrange
        var layout = CreateContainersLayout();
        var headerGrid = CreateGridWithInterleavedColumns(layout, TableLayoutRole.Header);
        var rowGrid = CreateGridWithLogicalColumns(layout, TableLayoutRole.Row);
        TableLayoutBehavior.Attach(headerGrid);
        TableLayoutBehavior.Attach(rowGrid);

        var callbackInvocations = 0;
        DependencyProperty? callbackProperty = null;
        DependencyObject? callbackSender = null;
        var callback = new DependencyPropertyChangedCallback((sender, changedProperty) =>
        {
            callbackInvocations++;
            callbackSender = sender;
            callbackProperty = changedProperty;
        });
        layout.RegisterPropertyChangedCallback(layout.GetWidthProperty(1), callback);

        // Act
        headerGrid.ColumnDefinitions[2].Width = new GridLength(260d, GridUnitType.Pixel);

        // Assert
        Assert.AreEqual(1, callbackInvocations);
        Assert.AreSame(layout, callbackSender);
        Assert.AreSame(layout.GetWidthProperty(1), callbackProperty);
        Assert.AreEqual(260d, layout.GetWidth(1).Value);
        AssertColumnWidthEquals(rowGrid.ColumnDefinitions[1], 260d);
    }

    [UITestMethod]
    public void Attach_CalledTwice_DoesNotCreateDuplicateSynchronization()
    {
        // Arrange
        var layout = CreateContainersLayout();
        var headerGrid = CreateGridWithInterleavedColumns(layout, TableLayoutRole.Header);
        var rowGrid = CreateGridWithLogicalColumns(layout, TableLayoutRole.Row);
        TableLayoutBehavior.Attach(headerGrid);
        TableLayoutBehavior.Attach(rowGrid);
        TableLayoutBehavior.Attach(headerGrid);
        TableLayoutBehavior.Attach(rowGrid);

        var callbackInvocations = 0;
        layout.RegisterPropertyChangedCallback(layout.GetWidthProperty(1), new DependencyPropertyChangedCallback((_, _) => callbackInvocations++));

        // Act
        headerGrid.ColumnDefinitions[2].Width = new GridLength(300d, GridUnitType.Pixel);

        // Assert
        Assert.AreEqual(1, callbackInvocations);
        Assert.AreEqual(300d, layout.GetWidth(1).Value);
        AssertColumnWidthEquals(headerGrid.ColumnDefinitions[2], 300d);
        AssertColumnWidthEquals(rowGrid.ColumnDefinitions[1], 300d);
    }

    [UITestMethod]
    public void HandleLoaded_LayoutAssigned_AttachesAndTracksLayoutChanges()
    {
        // Arrange
        var layout = CreateContainersLayout();
        var grid = CreateGridWithLogicalColumns(layout, TableLayoutRole.Row);

        // Act
        TableLayoutBehavior.HandleLoaded(grid);
        layout.SetWidth(1, new GridLength(240d, GridUnitType.Pixel));

        // Assert
        AssertColumnWidthEquals(grid.ColumnDefinitions[1], 240d);
    }

    [UITestMethod]
    public void HandleUnloaded_AttachedRow_StopsTrackingLayoutChanges()
    {
        // Arrange
        var layout = CreateContainersLayout();
        var grid = CreateGridWithLogicalColumns(layout, TableLayoutRole.Row);
        TableLayoutBehavior.HandleLoaded(grid);

        // Act
        TableLayoutBehavior.HandleUnloaded(grid);
        layout.SetWidth(1, new GridLength(240d, GridUnitType.Pixel));

        // Assert
        AssertColumnWidthEquals(grid.ColumnDefinitions[1], 130d);
    }

    [UITestMethod]
    public void HandleLoaded_AfterUnloaded_AppliesLatestLayout()
    {
        // Arrange
        var layout = CreateContainersLayout();
        var grid = CreateGridWithLogicalColumns(layout, TableLayoutRole.Row);
        TableLayoutBehavior.HandleLoaded(grid);
        TableLayoutBehavior.HandleUnloaded(grid);

        // Act
        layout.SetWidth(1, new GridLength(240d, GridUnitType.Pixel));
        TableLayoutBehavior.HandleLoaded(grid);

        // Assert
        AssertColumnWidthEquals(grid.ColumnDefinitions[1], 240d);
    }

    [UITestMethod]
    public void SetLayout_AttachedGrid_ReplacesLayoutAndSubscriptions()
    {
        // Arrange
        var firstLayout = CreateContainersLayout();
        var grid = CreateGridWithLogicalColumns(firstLayout, TableLayoutRole.Row);
        TableLayoutBehavior.Attach(grid);

        var secondLayout = CreateContainersLayout();
        secondLayout.SetWidth(1, new GridLength(250d, GridUnitType.Pixel));

        // Act
        TableLayoutBehavior.SetLayout(grid, secondLayout);
        firstLayout.SetWidth(1, new GridLength(280d, GridUnitType.Pixel));
        secondLayout.SetWidth(1, new GridLength(270d, GridUnitType.Pixel));

        // Assert
        AssertColumnWidthEquals(grid.ColumnDefinitions[1], 270d);
    }

    [UITestMethod]
    public void SetLayout_NullOnAttachedGrid_DetachesAndStopsTracking()
    {
        // Arrange
        var layout = CreateContainersLayout();
        var grid = CreateGridWithLogicalColumns(layout, TableLayoutRole.Row);
        TableLayoutBehavior.Attach(grid);

        // Act
        TableLayoutBehavior.SetLayout(grid, null);
        layout.SetWidth(1, new GridLength(240d, GridUnitType.Pixel));

        // Assert
        Assert.IsNull(TableLayoutBehavior.GetLayout(grid));
        AssertColumnWidthEquals(grid.ColumnDefinitions[1], 130d);
    }

    [UITestMethod]
    public void SetRole_AttachedGrid_ReconfiguresHeaderAuthority()
    {
        // Arrange
        var layout = CreateContainersLayout();
        var grid = CreateGridWithInterleavedColumns(layout, TableLayoutRole.Header);
        TableLayoutBehavior.Attach(grid);

        // Act
        TableLayoutBehavior.SetRole(grid, TableLayoutRole.Row);
        grid.ColumnDefinitions[2].Width = new GridLength(260d, GridUnitType.Pixel);

        // Assert
        Assert.AreEqual(130d, layout.GetWidth(1).Value);

        // Act
        TableLayoutBehavior.SetRole(grid, TableLayoutRole.Header);
        grid.ColumnDefinitions[2].Width = new GridLength(280d, GridUnitType.Pixel);

        // Assert
        Assert.AreEqual(280d, layout.GetWidth(1).Value);
        AssertColumnWidthEquals(grid.ColumnDefinitions[2], 280d);
    }

    [UITestMethod]
    public void HandleUnloaded_BeforeLoaded_IsIdempotent()
    {
        // Arrange
        var layout = CreateContainersLayout();
        var grid = CreateGridWithLogicalColumns(layout, TableLayoutRole.Row);

        // Act
        TableLayoutBehavior.HandleUnloaded(grid);
        layout.SetWidth(1, new GridLength(240d, GridUnitType.Pixel));

        // Assert
        AssertColumnWidthEquals(grid.ColumnDefinitions[1], 130d);
    }

    private static TableColumnLayout CreateContainersLayout()
    {
        return new TableColumnLayout(
            4,
            [
                new GridLength(1d, GridUnitType.Star),
                new GridLength(130d, GridUnitType.Pixel),
                new GridLength(120d, GridUnitType.Pixel),
                new GridLength(150d, GridUnitType.Pixel),
            ],
            [120d, 100d, 96d, 120d],
            [double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity],
            96d);
    }

    private static Grid CreateGridWithInterleavedColumns(TableColumnLayout layout, TableLayoutRole role)
    {
        var grid = new Grid();

        for (var index = 0; index < 8; index++)
        {
            grid.ColumnDefinitions.Add(CreateColumnDefinition(index % 2 == 0 ? index / 2 : -1));
        }

        TableLayoutBehavior.SetLayout(grid, layout);
        TableLayoutBehavior.SetRole(grid, role);

        return grid;
    }

    private static Grid CreateGridWithLogicalColumns(TableColumnLayout layout, TableLayoutRole role)
    {
        var grid = new Grid();

        for (var index = 0; index < 4; index++)
        {
            grid.ColumnDefinitions.Add(CreateColumnDefinition(index));
        }

        TableLayoutBehavior.SetLayout(grid, layout);
        TableLayoutBehavior.SetRole(grid, role);

        return grid;
    }

    private static ColumnDefinition CreateColumnDefinition(int logicalIndex)
    {
        var definition = new ColumnDefinition
        {
            Width = new GridLength(8d, GridUnitType.Pixel),
            MinWidth = 8d,
            MaxWidth = 8d,
        };

        TableLayoutBehavior.SetColumnIndex(definition, logicalIndex);
        return definition;
    }

    private static void AssertColumnMatchesLayout(ColumnDefinition column, TableColumnLayout layout, int logicalIndex)
    {
        var expectedWidth = layout.GetWidth(logicalIndex);
        Assert.AreEqual(expectedWidth.GridUnitType, column.Width.GridUnitType);
        Assert.AreEqual(expectedWidth.Value, column.Width.Value);
        Assert.AreEqual(layout.GetMinWidth(logicalIndex), column.MinWidth);
        Assert.AreEqual(layout.GetMaxWidth(logicalIndex), column.MaxWidth);
    }

    private static void AssertColumnWidthEquals(ColumnDefinition column, double expectedWidth)
    {
        Assert.AreEqual(GridUnitType.Pixel, column.Width.GridUnitType);
        Assert.AreEqual(expectedWidth, column.Width.Value);
    }

    private static void AssertColumnRemainsUnchanged(ColumnDefinition column, GridLength expectedWidth, double expectedMinWidth, double expectedMaxWidth)
    {
        Assert.AreEqual(expectedWidth.GridUnitType, column.Width.GridUnitType);
        Assert.AreEqual(expectedWidth.Value, column.Width.Value);
        Assert.AreEqual(expectedMinWidth, column.MinWidth);
        Assert.AreEqual(expectedMaxWidth, column.MaxWidth);
    }

}
