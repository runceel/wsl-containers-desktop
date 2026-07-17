using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using WslContainersDesktop_App.Tables;

namespace WslContainersDesktop_App_Tests.Tables;

[TestClass]
[DoNotParallelize]
public class TableColumnLayoutTests
{
    [UITestMethod]
    public void Create_DefaultLayout_UsesExpectedGridLengthValues()
    {
        // Arrange
        var layout = new TableColumnLayout();

        // Assert
        Assert.IsNotNull(layout);
        Assert.AreEqual(1, layout.ColumnCount);
        Assert.AreEqual(0d, layout.ActionRailWidth);

        var width = layout.GetWidth(0);
        Assert.AreEqual(GridUnitType.Star, width.GridUnitType);
        Assert.AreEqual(1d, width.Value);
        Assert.AreEqual(0d, layout.GetMinWidth(0));
        Assert.AreEqual(double.PositiveInfinity, layout.GetMaxWidth(0));
    }

    [UITestMethod]
    public void Create_Layout_IsAssignableToDependencyObject()
    {
        // Arrange
        var layout = new TableColumnLayout();

        // Assert
        Assert.IsInstanceOfType<DependencyObject>(layout);
    }

    [UITestMethod]
    public void Create_NetworksPreset_MapsEachLogicalColumnToDistinctWidthDependencyProperty()
    {
        // Arrange
        var layout = TableColumnLayoutCatalog.Create(TableLayoutPreset.Networks);
        var properties = new List<DependencyProperty>();

        // Act
        for (var index = 0; index < layout.ColumnCount; index++)
        {
            properties.Add(layout.GetWidthProperty(index));
        }

        // Assert
        Assert.AreEqual(layout.ColumnCount, properties.Count);
        CollectionAssert.AllItemsAreUnique(properties);
    }

    [UITestMethod]
    public void SetWidth_RegistersSingleNotificationForTargetColumn()
    {
        // Arrange
        var layout = TableColumnLayoutCatalog.Create(TableLayoutPreset.Networks);
        var widthProperty = layout.GetWidthProperty(1);

        var callbackInvocations = 0;
        DependencyProperty? receivedProperty = null;
        DependencyObject? sender = null;
        var callback = new DependencyPropertyChangedCallback((changedSender, changedProperty) =>
        {
            callbackInvocations++;
            sender = changedSender;
            receivedProperty = changedProperty;
        });

        // Act
        var callbackToken = layout.RegisterPropertyChangedCallback(widthProperty, callback);
        layout.SetWidth(1, new GridLength(240d, GridUnitType.Pixel));

        // Assert
        Assert.AreEqual(1, callbackInvocations);
        Assert.AreSame(widthProperty, receivedProperty);
        Assert.AreSame(layout, sender);
        Assert.AreEqual(GridUnitType.Pixel, layout.GetWidth(1).GridUnitType);
        Assert.AreEqual(240d, layout.GetWidth(1).Value);
        Assert.AreNotEqual(0L, callbackToken);
    }

    [UITestMethod]
    public void SetWidth_IndependentColumnsOnlyNotifyTheirOwnCallback()
    {
        // Arrange
        var layout = TableColumnLayoutCatalog.Create(TableLayoutPreset.Networks);
        var column0Property = layout.GetWidthProperty(0);
        var column1Property = layout.GetWidthProperty(1);

        var column0CallbackCount = 0;
        var column1CallbackCount = 0;

        var column0Callback = new DependencyPropertyChangedCallback((_, _) => column0CallbackCount++);
        var column1Callback = new DependencyPropertyChangedCallback((_, _) => column1CallbackCount++);

        // Act
        layout.RegisterPropertyChangedCallback(column0Property, column0Callback);
        layout.RegisterPropertyChangedCallback(column1Property, column1Callback);
        layout.SetWidth(1, new GridLength(240d, GridUnitType.Pixel));

        // Assert
        Assert.AreEqual(0, column0CallbackCount);
        Assert.AreEqual(1, column1CallbackCount);
    }

    [UITestMethod]
    public void SetWidth_UnregistersCallbackAndStopsFurtherNotifications()
    {
        // Arrange
        var layout = TableColumnLayoutCatalog.Create(TableLayoutPreset.Networks);
        var widthProperty = layout.GetWidthProperty(1);

        var callbackInvocations = 0;
        var callback = new DependencyPropertyChangedCallback((_, _) => callbackInvocations++);

        // Act
        var callbackToken = layout.RegisterPropertyChangedCallback(widthProperty, callback);
        layout.UnregisterPropertyChangedCallback(widthProperty, callbackToken);
        layout.SetWidth(1, new GridLength(260d, GridUnitType.Pixel));

        // Assert
        Assert.AreEqual(0, callbackInvocations);
    }

    [UITestMethod]
    public void Create_Preset_ReturnsExpectedLayout()
    {
        // Arrange
        var expectedLayouts = new[]
        {
            new ExpectedLayout(
                TableLayoutPreset.Containers,
                4,
                new[] { new GridLength(1d, GridUnitType.Star), new GridLength(130d, GridUnitType.Pixel), new GridLength(120d, GridUnitType.Pixel), new GridLength(150d, GridUnitType.Pixel) },
                new[] { 120d, 100d, 96d, 120d },
                new[] { double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity },
                96d),
            new ExpectedLayout(
                TableLayoutPreset.Images,
                4,
                new[] { new GridLength(2d, GridUnitType.Star), new GridLength(2d, GridUnitType.Star), new GridLength(1d, GridUnitType.Star), new GridLength(1d, GridUnitType.Star) },
                new[] { 140d, 160d, 80d, 120d },
                new[] { double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity },
                224d),
            new ExpectedLayout(
                TableLayoutPreset.Volumes,
                4,
                new[] { new GridLength(220d, GridUnitType.Pixel), new GridLength(120d, GridUnitType.Pixel), new GridLength(180d, GridUnitType.Pixel), new GridLength(1d, GridUnitType.Star) },
                new[] { 140d, 96d, 120d, 140d },
                new[] { double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity },
                120d),
            new ExpectedLayout(
                TableLayoutPreset.Networks,
                6,
                new[] { new GridLength(180d, GridUnitType.Pixel), new GridLength(120d, GridUnitType.Pixel), new GridLength(180d, GridUnitType.Pixel), new GridLength(96d, GridUnitType.Pixel), new GridLength(120d, GridUnitType.Pixel), new GridLength(1d, GridUnitType.Star) },
                new[] { 140d, 96d, 120d, 80d, 96d, 140d },
                new[] { double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity },
                120d),
            new ExpectedLayout(
                TableLayoutPreset.DashboardStats,
                3,
                new[] { new GridLength(1d, GridUnitType.Star), new GridLength(120d, GridUnitType.Pixel), new GridLength(200d, GridUnitType.Pixel) },
                new[] { 120d, 80d, 140d },
                new[] { double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity },
                192d)
        };

        // Act & Assert
        foreach (var expectedLayout in expectedLayouts)
        {
            var layout = TableColumnLayoutCatalog.Create(expectedLayout.Preset);

            Assert.IsNotNull(layout);
            Assert.AreEqual(expectedLayout.ColumnCount, layout.ColumnCount);
            Assert.AreEqual(expectedLayout.ActionRailWidth, layout.ActionRailWidth);

            for (var index = 0; index < expectedLayout.Widths.Length; index++)
            {
                var expectedWidth = expectedLayout.Widths[index];
                var actualWidth = layout.GetWidth(index);

                Assert.AreEqual(expectedWidth.GridUnitType, actualWidth.GridUnitType);
                Assert.AreEqual(expectedWidth.Value, actualWidth.Value);
            }

            for (var index = 0; index < expectedLayout.MinWidths.Length; index++)
            {
                Assert.AreEqual(expectedLayout.MinWidths[index], layout.GetMinWidth(index));
            }

            for (var index = 0; index < expectedLayout.MaxWidths.Length; index++)
            {
                Assert.AreEqual(expectedLayout.MaxWidths[index], layout.GetMaxWidth(index));
            }
        }
    }

    [UITestMethod]
    public void Create_CatalogCreateCalledTwice_ReturnsDistinctInstancesAndPreservesPresetDefaultWidth()
    {
        // Arrange
        var first = TableColumnLayoutCatalog.Create(TableLayoutPreset.Containers);
        var second = TableColumnLayoutCatalog.Create(TableLayoutPreset.Containers);

        // Act
        first.SetWidth(1, new GridLength(240d, GridUnitType.Pixel));

        // Assert
        Assert.AreNotSame(first, second);
        Assert.AreEqual(GridUnitType.Pixel, second.GetWidth(1).GridUnitType);
        Assert.AreEqual(130d, second.GetWidth(1).Value);
    }

    [UITestMethod]
    public void SetWidth_PixelWidthBelowMinimum_ClampsToMinimum()
    {
        // Arrange
        var layout = TableColumnLayoutCatalog.Create(TableLayoutPreset.Containers);

        // Act
        layout.SetWidth(1, new GridLength(10d, GridUnitType.Pixel));

        // Assert
        Assert.AreEqual(GridUnitType.Pixel, layout.GetWidth(1).GridUnitType);
        Assert.AreEqual(100d, layout.GetWidth(1).Value);
    }

    [UITestMethod]
    public void SetWidth_CustomFiniteMaximum_ClampsToMaximum()
    {
        // Arrange
        var layout = new TableColumnLayout(
            1,
            [new GridLength(100d, GridUnitType.Pixel)],
            [50d],
            [200d],
            0d);

        // Act
        layout.SetWidth(0, new GridLength(900d, GridUnitType.Pixel));

        // Assert
        var width = layout.GetWidth(0);
        Assert.AreEqual(GridUnitType.Pixel, width.GridUnitType);
        Assert.AreEqual(200d, width.Value);
    }

    [UITestMethod]
    public void SetWidth_PresetPixelWidthAboveFormerMaximum_DoesNotClamp()
    {
        // Arrange
        var layout = TableColumnLayoutCatalog.Create(TableLayoutPreset.Containers);

        // Act
        layout.SetWidth(1, new GridLength(900d, GridUnitType.Pixel));

        // Assert
        Assert.AreEqual(GridUnitType.Pixel, layout.GetWidth(1).GridUnitType);
        Assert.AreEqual(900d, layout.GetWidth(1).Value);
    }

    [UITestMethod]
    public void SetWidth_StarWidthPreservesUnitAndValue()
    {
        // Arrange
        var layout = TableColumnLayoutCatalog.Create(TableLayoutPreset.Images);

        // Act
        layout.SetWidth(0, new GridLength(3d, GridUnitType.Star));

        // Assert
        Assert.AreEqual(GridUnitType.Star, layout.GetWidth(0).GridUnitType);
        Assert.AreEqual(3d, layout.GetWidth(0).Value);
    }

    [UITestMethod]
    [DataRow(-1)]
    [DataRow(4)]
    public void GetWidth_InvalidIndex_ThrowsArgumentOutOfRangeException(int index)
    {
        // Arrange
        var layout = TableColumnLayoutCatalog.Create(TableLayoutPreset.Containers);

        // Act & Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => layout.GetWidth(index));
    }

    [UITestMethod]
    [DataRow(-1)]
    [DataRow(4)]
    public void GetMinWidth_InvalidIndex_ThrowsArgumentOutOfRangeException(int index)
    {
        // Arrange
        var layout = TableColumnLayoutCatalog.Create(TableLayoutPreset.Containers);

        // Act & Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => layout.GetMinWidth(index));
    }

    [UITestMethod]
    [DataRow(-1)]
    [DataRow(4)]
    public void GetMaxWidth_InvalidIndex_ThrowsArgumentOutOfRangeException(int index)
    {
        // Arrange
        var layout = TableColumnLayoutCatalog.Create(TableLayoutPreset.Containers);

        // Act & Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => layout.GetMaxWidth(index));
    }

    [UITestMethod]
    [DataRow(-1)]
    [DataRow(4)]
    public void SetWidth_RejectInvalidIndices(int index)
    {
        // Arrange
        var layout = TableColumnLayoutCatalog.Create(TableLayoutPreset.Containers);

        // Act & Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => layout.SetWidth(index, new GridLength(100d, GridUnitType.Pixel)));
    }

    [UITestMethod]
    public void SetWidth_AutoWidth_ThrowsArgumentException()
    {
        // Arrange
        var layout = TableColumnLayoutCatalog.Create(TableLayoutPreset.Containers);

        // Act & Assert
        Assert.ThrowsExactly<ArgumentException>(() => layout.SetWidth(1, GridLength.Auto));
    }

    [UITestMethod]
    public void Constructor_MismatchedCollectionLengths_ThrowsArgumentException()
    {
        // Arrange
        var widths = new[] { new GridLength(1d, GridUnitType.Star) };
        var minWidths = new[] { 100d, 200d };
        var maxWidths = new[] { 300d };

        // Act & Assert
        Assert.ThrowsExactly<ArgumentException>(() => new TableColumnLayout(2, widths, minWidths, maxWidths, 0d));
    }

    [UITestMethod]
    public void Constructor_NegativeMinWidth_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var widths = new[] { new GridLength(1d, GridUnitType.Star) };
        var minWidths = new[] { -1d };
        var maxWidths = new[] { 200d };

        // Act & Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TableColumnLayout(1, widths, minWidths, maxWidths, 0d));
    }

    [UITestMethod]
    public void Constructor_MinWidthGreaterThanMaxWidth_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var widths = new[] { new GridLength(1d, GridUnitType.Star) };
        var minWidths = new[] { 300d };
        var maxWidths = new[] { 200d };

        // Act & Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TableColumnLayout(1, widths, minWidths, maxWidths, 0d));
    }

    [UITestMethod]
    [DataRow(-1d)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void Constructor_ActionRailWidthOutOfRange_ThrowsArgumentOutOfRangeException(double actionRailWidth)
    {
        // Arrange
        var widths = new[] { new GridLength(1d, GridUnitType.Star) };
        var minWidths = new[] { 100d };
        var maxWidths = new[] { 200d };

        // Act & Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TableColumnLayout(1, widths, minWidths, maxWidths, actionRailWidth));
    }

    [TestMethod]
    public void ResourceKeys_AreExactAndDistinct()
    {
        // Arrange
        var expectedKeys = new[]
        {
            "ContainersTableColumnLayout",
            "ImagesTableColumnLayout",
            "VolumesTableColumnLayout",
            "NetworksTableColumnLayout",
            "DashboardStatsTableColumnLayout"
        };

        var actualKeys = new[]
        {
            TableColumnLayoutCatalog.ContainersResourceKey,
            TableColumnLayoutCatalog.ImagesResourceKey,
            TableColumnLayoutCatalog.VolumesResourceKey,
            TableColumnLayoutCatalog.NetworksResourceKey,
            TableColumnLayoutCatalog.DashboardStatsResourceKey
        };

        // Assert
        CollectionAssert.AreEqual(expectedKeys, actualKeys);
        CollectionAssert.AllItemsAreUnique(actualKeys);
    }

    private sealed record ExpectedLayout(
        TableLayoutPreset Preset,
        int ColumnCount,
        GridLength[] Widths,
        double[] MinWidths,
        double[] MaxWidths,
        double ActionRailWidth);
}
