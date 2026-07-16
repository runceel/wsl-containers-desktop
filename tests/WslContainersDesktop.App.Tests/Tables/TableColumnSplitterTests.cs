using System;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using WslContainersDesktop_App.Tables;
using Windows.Foundation;

namespace WslContainersDesktop_App_Tests.Tables;

[TestClass]
[DoNotParallelize]
public class TableColumnSplitterTests
{
    private const string BaseSplitterTypeName = "CommunityToolkit.WinUI.Controls.GridSplitter";

    [UITestMethod]
    public void Create_ConfiguresColumnResizeKeyboardAndFocusDefaults()
    {
        // Act
        var splitter = new TableColumnSplitter();

        // Assert
        Assert.AreEqual(BaseSplitterTypeName, typeof(TableColumnSplitter).BaseType?.FullName);
        Assert.AreEqual(GridSplitter.GridResizeDirection.Columns, splitter.ResizeDirection);
        Assert.AreEqual(8d, splitter.KeyboardIncrement);
        Assert.IsTrue(splitter.IsTabStop);
        Assert.IsTrue(splitter.UseSystemFocusVisuals);
    }

    [UITestMethod]
    public async Task ResizeColumns_FirstBoundary_PositiveDelta_ResizesTrailingColumnsAndKeepsStarLayout()
    {
        await RunOnUIThreadAsync(async () =>
        {
            using var fixture = await SplitterHostFixture.CreateAsync(splitterColumnIndex: 1);

            // Act
            var resized = fixture.Splitter.ResizeColumns(60d);
            await fixture.RefreshLayoutAsync();

            // Assert
            Assert.IsTrue(resized);
            Assert.AreEqual(260d, fixture.GetDataColumn(0).ActualWidth, 0.001d);
            Assert.AreEqual(100d, fixture.GetDataColumn(1).ActualWidth, 0.001d);
            Assert.AreEqual(96d, fixture.GetDataColumn(2).ActualWidth, 0.001d);
            Assert.AreEqual(144d, fixture.GetDataColumn(3).ActualWidth, 0.001d);
            Assert.AreEqual(GridUnitType.Star, fixture.Layout.GetWidth(0).GridUnitType);
            Assert.AreEqual(1d, fixture.Layout.GetWidth(0).Value, 0.001d);
        });
    }

    [UITestMethod]
    public async Task BeginResize_ContinueResize_FirstBoundary_UsesImmutableBaselineAcrossCumulativeDeltas()
    {
        await RunOnUIThreadAsync(async () =>
        {
            using var fixture = await SplitterHostFixture.CreateAsync(splitterColumnIndex: 1);

            // Act
            fixture.Splitter.BeginResize();

            var resized = fixture.Splitter.ContinueResize(20d);
            await fixture.RefreshLayoutAsync();

            // Assert
            Assert.IsTrue(resized);
            Assert.AreEqual(220d, fixture.GetDataColumn(0).ActualWidth, 0.001d);
            Assert.AreEqual(110d, fixture.GetDataColumn(1).ActualWidth, 0.001d);
            Assert.AreEqual(120d, fixture.GetDataColumn(2).ActualWidth, 0.001d);
            Assert.AreEqual(150d, fixture.GetDataColumn(3).ActualWidth, 0.001d);

            resized = fixture.Splitter.ContinueResize(100d);
            await fixture.RefreshLayoutAsync();

            Assert.IsTrue(resized);
            Assert.AreEqual(284d, fixture.GetDataColumn(0).ActualWidth, 0.001d);
            Assert.AreEqual(100d, fixture.GetDataColumn(1).ActualWidth, 0.001d);
            Assert.AreEqual(96d, fixture.GetDataColumn(2).ActualWidth, 0.001d);
            Assert.AreEqual(120d, fixture.GetDataColumn(3).ActualWidth, 0.001d);

            resized = fixture.Splitter.ContinueResize(40d);
            await fixture.RefreshLayoutAsync();

            Assert.IsTrue(resized);
            Assert.AreEqual(240d, fixture.GetDataColumn(0).ActualWidth, 0.001d);
            Assert.AreEqual(100d, fixture.GetDataColumn(1).ActualWidth, 0.001d);
            Assert.AreEqual(110d, fixture.GetDataColumn(2).ActualWidth, 0.001d);
            Assert.AreEqual(150d, fixture.GetDataColumn(3).ActualWidth, 0.001d);

            resized = fixture.Splitter.ContinueResize(-20d);
            await fixture.RefreshLayoutAsync();

            Assert.IsTrue(resized);
            Assert.AreEqual(180d, fixture.GetDataColumn(0).ActualWidth, 0.001d);
            Assert.AreEqual(150d, fixture.GetDataColumn(1).ActualWidth, 0.001d);
            Assert.AreEqual(120d, fixture.GetDataColumn(2).ActualWidth, 0.001d);
            Assert.AreEqual(150d, fixture.GetDataColumn(3).ActualWidth, 0.001d);
            Assert.AreEqual(GridUnitType.Star, fixture.Layout.GetWidth(0).GridUnitType);
            Assert.AreEqual(GridUnitType.Pixel, fixture.Layout.GetWidth(1).GridUnitType);
            Assert.AreEqual(GridUnitType.Pixel, fixture.Layout.GetWidth(2).GridUnitType);
            Assert.AreEqual(GridUnitType.Pixel, fixture.Layout.GetWidth(3).GridUnitType);
        });
    }

    [UITestMethod]
    public async Task ResizeColumns_FirstBoundary_PositiveDelta_Overdrag_ClampsToNearestLimitAndStopsFurtherSameDirectionResize()
    {
        await RunOnUIThreadAsync(async () =>
        {
            using var fixture = await SplitterHostFixture.CreateAsync(splitterColumnIndex: 1);

            // Act
            var resized = fixture.Splitter.ResizeColumns(100d);
            await fixture.RefreshLayoutAsync();

            // Assert
            Assert.IsTrue(resized);
            Assert.AreEqual(284d, fixture.GetDataColumn(0).ActualWidth, 0.001d);
            Assert.AreEqual(100d, fixture.GetDataColumn(1).ActualWidth, 0.001d);
            Assert.AreEqual(96d, fixture.GetDataColumn(2).ActualWidth, 0.001d);
            Assert.AreEqual(120d, fixture.GetDataColumn(3).ActualWidth, 0.001d);

            // Act
            var resizedAgain = fixture.Splitter.ResizeColumns(8d);
            await fixture.RefreshLayoutAsync();

            // Assert
            Assert.IsFalse(resizedAgain);
            Assert.AreEqual(284d, fixture.GetDataColumn(0).ActualWidth, 0.001d);
            Assert.AreEqual(100d, fixture.GetDataColumn(1).ActualWidth, 0.001d);
            Assert.AreEqual(96d, fixture.GetDataColumn(2).ActualWidth, 0.001d);
            Assert.AreEqual(120d, fixture.GetDataColumn(3).ActualWidth, 0.001d);
        });
    }

    [UITestMethod]
    public async Task ResizeColumns_FirstBoundary_NegativeDelta_ClampsToLeftMinimumAndKeepsStarLayout()
    {
        await RunOnUIThreadAsync(async () =>
        {
            using var fixture = await SplitterHostFixture.CreateAsync(splitterColumnIndex: 1);

            // Act
            var resized = fixture.Splitter.ResizeColumns(-100d);
            await fixture.RefreshLayoutAsync();

            // Assert
            Assert.IsTrue(resized);
            Assert.AreEqual(120d, fixture.GetDataColumn(0).ActualWidth, 0.001d);
            Assert.AreEqual(210d, fixture.GetDataColumn(1).ActualWidth, 0.001d);
            Assert.AreEqual(120d, fixture.GetDataColumn(2).ActualWidth, 0.001d);
            Assert.AreEqual(150d, fixture.GetDataColumn(3).ActualWidth, 0.001d);
            Assert.AreEqual(GridUnitType.Star, fixture.Layout.GetWidth(0).GridUnitType);
            Assert.AreEqual(1d, fixture.Layout.GetWidth(0).Value, 0.001d);
        });
    }

    [UITestMethod]
    public async Task ResizeColumns_SecondBoundary_PositiveDelta_ClampsToTrailingAggregateCapacity()
    {
        await RunOnUIThreadAsync(async () =>
        {
            using var fixture = await SplitterHostFixture.CreateAsync(splitterColumnIndex: 3);

            // Act
            var resized = fixture.Splitter.ResizeColumns(80d);
            await fixture.RefreshLayoutAsync();

            // Assert
            Assert.IsTrue(resized);
            Assert.AreEqual(200d, fixture.GetDataColumn(0).ActualWidth, 0.001d);
            Assert.AreEqual(184d, fixture.GetDataColumn(1).ActualWidth, 0.001d);
            Assert.AreEqual(96d, fixture.GetDataColumn(2).ActualWidth, 0.001d);
            Assert.AreEqual(120d, fixture.GetDataColumn(3).ActualWidth, 0.001d);
        });
    }

    [UITestMethod]
    public async Task ResizeColumns_SecondBoundary_PositiveDelta_UsesNearestFirstOrderWhenSpilling()
    {
        await RunOnUIThreadAsync(async () =>
        {
            using var fixture = await SplitterHostFixture.CreateAsync(splitterColumnIndex: 3);

            // Act
            var resized = fixture.Splitter.ResizeColumns(40d);
            await fixture.RefreshLayoutAsync();

            // Assert
            Assert.IsTrue(resized);
            Assert.AreEqual(200d, fixture.GetDataColumn(0).ActualWidth, 0.001d);
            Assert.AreEqual(170d, fixture.GetDataColumn(1).ActualWidth, 0.001d);
            Assert.AreEqual(96d, fixture.GetDataColumn(2).ActualWidth, 0.001d);
            Assert.AreEqual(134d, fixture.GetDataColumn(3).ActualWidth, 0.001d);
        });
    }

    [UITestMethod]
    public async Task ResizeColumns_LastBoundary_PositiveDelta_ClampsToTrailingCapacity()
    {
        await RunOnUIThreadAsync(async () =>
        {
            using var fixture = await SplitterHostFixture.CreateAsync(splitterColumnIndex: 5);

            // Act
            var resized = fixture.Splitter.ResizeColumns(50d);
            await fixture.RefreshLayoutAsync();

            // Assert
            Assert.IsTrue(resized);
            Assert.AreEqual(150d, fixture.GetDataColumn(2).ActualWidth, 0.001d);
            Assert.AreEqual(120d, fixture.GetDataColumn(3).ActualWidth, 0.001d);
        });
    }

    [UITestMethod]
    public async Task ResizeColumns_LastBoundary_NegativeDelta_UsesNearestLeadingColumnsWhenSpilling()
    {
        await RunOnUIThreadAsync(async () =>
        {
            using var fixture = await SplitterHostFixture.CreateAsync(splitterColumnIndex: 5);

            // Act
            var resized = fixture.Splitter.ResizeColumns(-40d);
            await fixture.RefreshLayoutAsync();

            // Assert
            Assert.IsTrue(resized);
            Assert.AreEqual(200d, fixture.GetDataColumn(0).ActualWidth, 0.001d);
            Assert.AreEqual(114d, fixture.GetDataColumn(1).ActualWidth, 0.001d);
            Assert.AreEqual(96d, fixture.GetDataColumn(2).ActualWidth, 0.001d);
            Assert.AreEqual(190d, fixture.GetDataColumn(3).ActualWidth, 0.001d);
            Assert.AreEqual(600d, fixture.GetDataColumn(0).ActualWidth + fixture.GetDataColumn(1).ActualWidth + fixture.GetDataColumn(2).ActualWidth + fixture.GetDataColumn(3).ActualWidth, 0.001d);
            Assert.AreEqual(GridUnitType.Star, fixture.Layout.GetWidth(0).GridUnitType);
            Assert.AreEqual(GridUnitType.Pixel, fixture.Layout.GetWidth(1).GridUnitType);
            Assert.AreEqual(GridUnitType.Pixel, fixture.Layout.GetWidth(2).GridUnitType);
            Assert.AreEqual(GridUnitType.Pixel, fixture.Layout.GetWidth(3).GridUnitType);
        });
    }

    [UITestMethod]
    public async Task ResizeColumns_FirstBoundary_NegativeDelta_Regression_UsesRightmostCreatedColumnWhenReclaimingNameAfterMinimums()
    {
        await RunOnUIThreadAsync(async () =>
        {
            using var fixture = await SplitterHostFixture.CreateAsync(splitterColumnIndex: 1);

            // Act
            var resized = fixture.Splitter.ResizeColumns(100d);
            await fixture.RefreshLayoutAsync();

            // Assert
            Assert.IsTrue(resized);
            Assert.AreEqual(284d, fixture.GetDataColumn(0).ActualWidth, 0.001d);
            Assert.AreEqual(100d, fixture.GetDataColumn(1).ActualWidth, 0.001d);
            Assert.AreEqual(96d, fixture.GetDataColumn(2).ActualWidth, 0.001d);
            Assert.AreEqual(120d, fixture.GetDataColumn(3).ActualWidth, 0.001d);

            // Act
            Grid.SetColumn(fixture.Splitter, 5);
            await fixture.RefreshLayoutAsync();
            resized = fixture.Splitter.ResizeColumns(-40d);
            await fixture.RefreshLayoutAsync();

            // Assert
            Assert.IsTrue(resized);
            Assert.AreEqual(244d, fixture.GetDataColumn(0).ActualWidth, 0.001d);
            Assert.AreEqual(100d, fixture.GetDataColumn(1).ActualWidth, 0.001d);
            Assert.AreEqual(96d, fixture.GetDataColumn(2).ActualWidth, 0.001d);
            Assert.AreEqual(160d, fixture.GetDataColumn(3).ActualWidth, 0.001d);
            Assert.AreEqual(600d, fixture.GetDataColumn(0).ActualWidth + fixture.GetDataColumn(1).ActualWidth + fixture.GetDataColumn(2).ActualWidth + fixture.GetDataColumn(3).ActualWidth, 0.001d);
            Assert.AreEqual(GridUnitType.Star, fixture.Layout.GetWidth(0).GridUnitType);
            Assert.AreEqual(GridUnitType.Pixel, fixture.Layout.GetWidth(1).GridUnitType);
            Assert.AreEqual(GridUnitType.Pixel, fixture.Layout.GetWidth(2).GridUnitType);
            Assert.AreEqual(GridUnitType.Pixel, fixture.Layout.GetWidth(3).GridUnitType);
        });
    }

    [UITestMethod]
    public async Task ResizeColumns_LastBoundary_PositiveDelta_UsesNearestLeadingColumnsWhenAdjacentIsAtMaximum()
    {
        await RunOnUIThreadAsync(async () =>
        {
            using var fixture = await SplitterHostFixture.CreateAsync(splitterColumnIndex: 1);

            // Act
            var resized = fixture.Splitter.ResizeColumns(-80d);
            await fixture.RefreshLayoutAsync();

            // Assert
            Assert.IsTrue(resized);
            Assert.AreEqual(120d, fixture.GetDataColumn(0).ActualWidth, 0.001d);
            Assert.AreEqual(210d, fixture.GetDataColumn(1).ActualWidth, 0.001d);
            Assert.AreEqual(120d, fixture.GetDataColumn(2).ActualWidth, 0.001d);
            Assert.AreEqual(150d, fixture.GetDataColumn(3).ActualWidth, 0.001d);

            // Act
            Grid.SetColumn(fixture.Splitter, 3);
            resized = fixture.Splitter.ResizeColumns(-110d);
            await fixture.RefreshLayoutAsync();

            // Assert
            Assert.IsTrue(resized);
            Assert.AreEqual(120d, fixture.GetDataColumn(0).ActualWidth, 0.001d);
            Assert.AreEqual(100d, fixture.GetDataColumn(1).ActualWidth, 0.001d);
            Assert.AreEqual(230d, fixture.GetDataColumn(2).ActualWidth, 0.001d);
            Assert.AreEqual(150d, fixture.GetDataColumn(3).ActualWidth, 0.001d);

            // Act
            Grid.SetColumn(fixture.Splitter, 5);
            resized = fixture.Splitter.ResizeColumns(10d);
            await fixture.RefreshLayoutAsync();

            // Assert
            Assert.IsTrue(resized);
            Assert.AreEqual(120d, fixture.GetDataColumn(0).ActualWidth, 0.001d);
            Assert.AreEqual(100d, fixture.GetDataColumn(1).ActualWidth, 0.001d);
            Assert.AreEqual(240d, fixture.GetDataColumn(2).ActualWidth, 0.001d);
            Assert.AreEqual(140d, fixture.GetDataColumn(3).ActualWidth, 0.001d);

            // Act
            resized = fixture.Splitter.ResizeColumns(20d);
            await fixture.RefreshLayoutAsync();

            // Assert
            Assert.IsTrue(resized);
            Assert.AreEqual(120d, fixture.GetDataColumn(0).ActualWidth, 0.001d);
            Assert.AreEqual(120d, fixture.GetDataColumn(1).ActualWidth, 0.001d);
            Assert.AreEqual(240d, fixture.GetDataColumn(2).ActualWidth, 0.001d);
            Assert.AreEqual(120d, fixture.GetDataColumn(3).ActualWidth, 0.001d);
            Assert.AreEqual(600d, fixture.GetDataColumn(0).ActualWidth + fixture.GetDataColumn(1).ActualWidth + fixture.GetDataColumn(2).ActualWidth + fixture.GetDataColumn(3).ActualWidth, 0.001d);
            Assert.AreEqual(GridUnitType.Star, fixture.Layout.GetWidth(0).GridUnitType);
            Assert.AreEqual(GridUnitType.Pixel, fixture.Layout.GetWidth(1).GridUnitType);
            Assert.AreEqual(GridUnitType.Pixel, fixture.Layout.GetWidth(2).GridUnitType);
            Assert.AreEqual(GridUnitType.Pixel, fixture.Layout.GetWidth(3).GridUnitType);
        });
    }

    [UITestMethod]
    public async Task ResizeColumns_ImagesPreset_SecondBoundaryPositiveDelta_ClampsToTrailingAggregateCapacityAndKeepsStarLayout()
    {
        await RunOnUIThreadAsync(async () =>
        {
            using var fixture = await SplitterHostFixture.CreateAsync(
                preset: TableLayoutPreset.Images,
                hostWidth: 744d,
                expectedInitialActualWidths: [240d, 240d, 120d, 120d],
                splitterColumnIndex: 3);

            // Act
            var resized = fixture.Splitter.ResizeColumns(100d);
            await fixture.RefreshLayoutAsync();

            // Assert
            Assert.IsTrue(resized);
            Assert.AreEqual(240d, fixture.GetDataColumn(0).ActualWidth, 0.001d);
            Assert.AreEqual(280d, fixture.GetDataColumn(1).ActualWidth, 0.001d);
            Assert.AreEqual(80d, fixture.GetDataColumn(2).ActualWidth, 0.001d);
            Assert.AreEqual(120d, fixture.GetDataColumn(3).ActualWidth, 0.001d);
            Assert.AreEqual(GridUnitType.Star, fixture.Layout.GetWidth(0).GridUnitType);
            Assert.AreEqual(GridUnitType.Star, fixture.Layout.GetWidth(1).GridUnitType);
            Assert.AreEqual(GridUnitType.Star, fixture.Layout.GetWidth(2).GridUnitType);
            Assert.AreEqual(GridUnitType.Star, fixture.Layout.GetWidth(3).GridUnitType);
        });
    }

    [UITestMethod]
    public async Task ResizeColumns_VolumesPreset_LastBoundaryPositiveDelta_ClampsToTrailingCapacityAndKeepsStarLayout()
    {
        await RunOnUIThreadAsync(async () =>
        {
            using var fixture = await SplitterHostFixture.CreateAsync(
                preset: TableLayoutPreset.Volumes,
                hostWidth: 744d,
                expectedInitialActualWidths: [220d, 120d, 180d, 200d],
                splitterColumnIndex: 5);

            // Act
            var resized = fixture.Splitter.ResizeColumns(80d);
            await fixture.RefreshLayoutAsync();

            // Assert
            Assert.IsTrue(resized);
            Assert.AreEqual(220d, fixture.GetDataColumn(0).ActualWidth, 0.001d);
            Assert.AreEqual(120d, fixture.GetDataColumn(1).ActualWidth, 0.001d);
            Assert.AreEqual(240d, fixture.GetDataColumn(2).ActualWidth, 0.001d);
            Assert.AreEqual(140d, fixture.GetDataColumn(3).ActualWidth, 0.001d);
            Assert.AreEqual(GridUnitType.Star, fixture.Layout.GetWidth(3).GridUnitType);
        });
    }

    [UITestMethod]
    public void Create_SetsKeyboardFocusableAndAccessibleName()
    {
        // Arrange
        var splitter = new TableColumnSplitter();

        // Act
        AutomationProperties.SetName(splitter, "Table splitter");
        AutomationProperties.SetAutomationId(splitter, "TableColumnSplitter");

        // Assert
        Assert.IsTrue(splitter.IsTabStop);
        Assert.IsTrue(splitter.UseSystemFocusVisuals);
        Assert.AreEqual("Table splitter", AutomationProperties.GetName(splitter));
        Assert.AreEqual("TableColumnSplitter", AutomationProperties.GetAutomationId(splitter));
    }

    [UITestMethod]
    public void Create_SetsAutomationPeerWithAssignedName()
    {
        // Arrange
        var splitter = new TableColumnSplitter();

        // Act
        AutomationProperties.SetName(splitter, "Table splitter");
        var peer = FrameworkElementAutomationPeer.CreatePeerForElement(splitter);

        // Assert
        Assert.IsNotNull(peer, "Expected the automation peer to be created without a loaded window.");
        Assert.AreEqual("Table splitter", peer!.GetName());
    }

    private static async Task RunOnUIThreadAsync(Func<Task> action)
    {
        var dispatcherQueue = DispatcherQueue.GetForCurrentThread() ?? UITestMethodAttribute.DispatcherQueue;
        Assert.IsNotNull(dispatcherQueue, "Expected a dispatcher queue to be initialized for the current UI test thread.");

        var completionSource = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcherQueue!.TryEnqueue(async () =>
        {
            try
            {
                await action();
                completionSource.TrySetResult(null);
            }
            catch (Exception ex)
            {
                completionSource.TrySetException(ex);
            }
        });

        await completionSource.Task;
    }

    private static Task YieldToDispatcherAsync()
    {
        var dispatcherQueue = DispatcherQueue.GetForCurrentThread() ?? UITestMethodAttribute.DispatcherQueue;
        Assert.IsNotNull(dispatcherQueue, "Expected a dispatcher queue to be initialized for the current UI test thread.");

        var completionSource = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcherQueue!.TryEnqueue(() => completionSource.SetResult(null));
        return completionSource.Task;
    }

    private static Task WaitForLoadedAsync(FrameworkElement element)
    {
        if (element.IsLoaded)
        {
            return Task.CompletedTask;
        }

        var completionSource = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        RoutedEventHandler? handler = null;
        handler = (_, _) =>
        {
            element.Loaded -= handler;
            completionSource.TrySetResult(null);
        };
        element.Loaded += handler;
        return completionSource.Task;
    }

    private sealed class SplitterHostFixture : IDisposable
    {
        private const double HostHeight = 40d;

        private Window? _window;
        private readonly TableColumnLayout _layout;
        private readonly double[] _expectedInitialActualWidths;

        private SplitterHostFixture(
            Window window,
            Grid host,
            Grid grid,
            TableColumnSplitter splitter,
            TableColumnLayout layout,
            double[] expectedInitialActualWidths)
        {
            _window = window;
            Host = host;
            SplitterGrid = grid;
            Splitter = splitter;
            _layout = layout;
            _expectedInitialActualWidths = expectedInitialActualWidths;
        }

        private Grid Host { get; }

        public Grid SplitterGrid { get; }

        public TableColumnSplitter Splitter { get; }

        public TableColumnLayout Layout => _layout;

        public ColumnDefinition GetDataColumn(int logicalIndex) => SplitterGrid.ColumnDefinitions[logicalIndex * 2];

        public static Task<SplitterHostFixture> CreateAsync(int splitterColumnIndex)
            => CreateAsync(
                preset: TableLayoutPreset.Containers,
                hostWidth: 624d,
                expectedInitialActualWidths: [200d, 130d, 120d, 150d],
                splitterColumnIndex: splitterColumnIndex);

        public static async Task<SplitterHostFixture> CreateAsync(
            TableLayoutPreset preset,
            double hostWidth,
            double[] expectedInitialActualWidths,
            int splitterColumnIndex)
        {
            ArgumentNullException.ThrowIfNull(expectedInitialActualWidths);

            var layout = TableColumnLayoutCatalog.Create(preset);
            if (expectedInitialActualWidths.Length != layout.ColumnCount)
            {
                throw new ArgumentException("Expected initial actual widths must match the layout column count.", nameof(expectedInitialActualWidths));
            }

            var grid = new Grid
            {
                Width = hostWidth,
                Height = HostHeight,
            };

            var physicalColumnCount = layout.ColumnCount * 2 - 1;
            for (var physicalIndex = 0; physicalIndex < physicalColumnCount; physicalIndex++)
            {
                if (physicalIndex % 2 == 0)
                {
                    var logicalIndex = physicalIndex / 2;
                    var column = new ColumnDefinition
                    {
                        Width = layout.GetWidth(logicalIndex),
                        MinWidth = layout.GetMinWidth(logicalIndex),
                        MaxWidth = layout.GetMaxWidth(logicalIndex),
                    };
                    TableLayoutBehavior.SetColumnIndex(column, logicalIndex);
                    grid.ColumnDefinitions.Add(column);
                }
                else
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8d, GridUnitType.Pixel) });
                }
            }

            var splitter = new TableColumnSplitter();
            Grid.SetColumn(splitter, splitterColumnIndex);
            grid.Children.Add(splitter);

            TableLayoutBehavior.SetLayout(grid, layout);
            TableLayoutBehavior.SetRole(grid, TableLayoutRole.Header);
            TableLayoutBehavior.Attach(grid);

            var host = new Grid
            {
                Width = hostWidth,
                Height = HostHeight,
            };
            host.Children.Add(grid);

            var window = new Window();
            var fixture = new SplitterHostFixture(window, host, grid, splitter, layout, expectedInitialActualWidths);
            try
            {
                window.Content = host;
                window.Activate();
                await WaitForLoadedAsync(host);
                await WaitForLoadedAsync(splitter);
                await YieldToDispatcherAsync();
                await YieldToDispatcherAsync();
                await EnsureLayoutAsync(host);
                fixture.AssertPreconditions();
                return fixture;
            }
            catch
            {
                fixture.Dispose();
                throw;
            }
        }

        public async Task RefreshLayoutAsync()
        {
            await YieldToDispatcherAsync();
            await EnsureLayoutAsync(Host);
        }

        public void Dispose()
        {
            if (_window is null)
            {
                return;
            }

            _window.Content = null;
            _window.Close();
            _window = null;
        }

        private void AssertPreconditions()
        {
            for (var logicalIndex = 0; logicalIndex < _expectedInitialActualWidths.Length; logicalIndex++)
            {
                Assert.AreEqual(_expectedInitialActualWidths[logicalIndex], GetDataColumn(logicalIndex).ActualWidth, 0.001d);
            }
        }

        private static Task EnsureLayoutAsync(FrameworkElement element)
        {
            var completionSource = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            element.DispatcherQueue.TryEnqueue(() =>
            {
                element.UpdateLayout();
                element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                element.Arrange(new Rect(0, 0, element.DesiredSize.Width, element.DesiredSize.Height));
                completionSource.TrySetResult(null);
            });
            return completionSource.Task;
        }
    }
}
