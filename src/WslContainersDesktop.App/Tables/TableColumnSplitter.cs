using System;
using System.Collections.Generic;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WslContainersDesktop_App.Tables;

/// <summary>
/// Represents a splitter that redistributes width between the logical table columns on both sides of the splitter.
/// </summary>
public sealed class TableColumnSplitter : GridSplitter
{
    private ResizeSession? _activeSession;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableColumnSplitter"/> class.
    /// </summary>
    public TableColumnSplitter()
    {
        ResizeDirection = GridResizeDirection.Columns;
        ResizeBehavior = GridResizeBehavior.PreviousAndNext;
        KeyboardIncrement = 8d;
        IsTabStop = true;
        UseSystemFocusVisuals = true;
    }

    internal void BeginResize()
    {
        OnDragStarting();
    }

    internal bool ContinueResize(double horizontalChange)
    {
        return OnDragHorizontal(horizontalChange);
    }

    internal bool ResizeColumns(double horizontalChange)
    {
        BeginResize();
        return ContinueResize(horizontalChange);
    }

    protected override void OnDragStarting()
    {
        base.OnDragStarting();
        _activeSession = ResizeSession.Create(this);
    }

    protected override bool OnDragHorizontal(double horizontalChange)
    {
        if (_activeSession is null)
        {
            return false;
        }

        return _activeSession.Apply(horizontalChange);
    }

    private sealed class ResizeSession
    {
        private readonly List<ColumnSnapshot> _dataColumns;
        private readonly List<ColumnSnapshot> _leadingColumns;
        private readonly List<ColumnSnapshot> _trailingColumns;

        private ResizeSession(
            List<ColumnSnapshot> dataColumns,
            List<ColumnSnapshot> leadingColumns,
            List<ColumnSnapshot> trailingColumns)
        {
            _dataColumns = dataColumns;
            _leadingColumns = leadingColumns;
            _trailingColumns = trailingColumns;
        }

        public static ResizeSession? Create(TableColumnSplitter splitter)
        {
            if (splitter.Parent is not Grid grid)
            {
                return null;
            }

            var splitterPhysicalIndex = Grid.GetColumn(splitter);
            if (splitterPhysicalIndex < 0)
            {
                return null;
            }

            var dataColumns = new List<ColumnSnapshot>();
            for (var physicalIndex = 0; physicalIndex < grid.ColumnDefinitions.Count; physicalIndex++)
            {
                var column = grid.ColumnDefinitions[physicalIndex];
                if (TableLayoutBehavior.GetColumnIndex(column) < 0)
                {
                    continue;
                }

                if (!ColumnSnapshot.TryCreate(column, physicalIndex, out var snapshot))
                {
                    return null;
                }

                dataColumns.Add(snapshot);
            }

            if (dataColumns.Count == 0)
            {
                return null;
            }

            var leadingColumns = new List<ColumnSnapshot>();
            var trailingColumns = new List<ColumnSnapshot>();
            foreach (var dataColumn in dataColumns)
            {
                if (dataColumn.PhysicalIndex < splitterPhysicalIndex)
                {
                    leadingColumns.Add(dataColumn);
                }
                else if (dataColumn.PhysicalIndex > splitterPhysicalIndex)
                {
                    trailingColumns.Add(dataColumn);
                }
            }

            if (leadingColumns.Count == 0 || trailingColumns.Count == 0)
            {
                return null;
            }

            return new ResizeSession(dataColumns, leadingColumns, trailingColumns);
        }

        public bool Apply(double horizontalChange)
        {
            var targetWidths = new Dictionary<ColumnDefinition, double>();
            foreach (var column in _dataColumns)
            {
                targetWidths[column.ColumnDefinition] = column.BaselineActualWidth;
            }

            var expandLeadingColumns = horizontalChange > 0d;
            var requestedMagnitude = Math.Abs(horizontalChange);
            var leadingCapacity = expandLeadingColumns
                ? GetExpansionCapacity(_leadingColumns)
                : GetShrinkCapacity(_leadingColumns);
            var trailingCapacity = expandLeadingColumns
                ? GetShrinkCapacity(_trailingColumns)
                : GetExpansionCapacity(_trailingColumns);

            var effectiveMagnitude = Math.Min(requestedMagnitude, leadingCapacity);
            effectiveMagnitude = Math.Min(effectiveMagnitude, trailingCapacity);
            if (effectiveMagnitude <= 0d)
            {
                return false;
            }

            if (expandLeadingColumns)
            {
                ExpandNearestFirst(_leadingColumns, effectiveMagnitude, targetWidths, nearestColumnIsLast: true);
                ShrinkNearestFirst(_trailingColumns, effectiveMagnitude, targetWidths, nearestColumnIsLast: false);
            }
            else
            {
                ShrinkNearestFirst(_leadingColumns, effectiveMagnitude, targetWidths, nearestColumnIsLast: true);
                ExpandNearestFirst(_trailingColumns, effectiveMagnitude, targetWidths, nearestColumnIsLast: false);
            }

            var starColumns = new List<ColumnSnapshot>();
            foreach (var column in _dataColumns)
            {
                if (column.UnitType == GridUnitType.Star)
                {
                    starColumns.Add(column);
                    continue;
                }

                if (column.UnitType == GridUnitType.Pixel)
                {
                    var desiredWidth = targetWidths[column.ColumnDefinition];
                    if (column.ColumnDefinition.Width.GridUnitType == GridUnitType.Pixel && column.ColumnDefinition.Width.Value != desiredWidth)
                    {
                        column.ColumnDefinition.Width = new GridLength(desiredWidth, GridUnitType.Pixel);
                    }
                }
            }

            if (starColumns.Count > 1)
            {
                foreach (var starColumn in starColumns)
                {
                    starColumn.ColumnDefinition.Width = new GridLength(targetWidths[starColumn.ColumnDefinition], GridUnitType.Star);
                }
            }

            return true;
        }

        private static double GetExpansionCapacity(IReadOnlyList<ColumnSnapshot> columns)
        {
            var capacity = 0d;
            foreach (var column in columns)
            {
                capacity += GetExpansionCapacity(column);
            }

            return capacity;
        }

        private static double GetExpansionCapacity(ColumnSnapshot column)
            => ClampToZero(column.MaxWidth - column.BaselineActualWidth);

        private static double GetShrinkCapacity(IReadOnlyList<ColumnSnapshot> columns)
        {
            var capacity = 0d;
            foreach (var column in columns)
            {
                capacity += GetShrinkCapacity(column);
            }

            return capacity;
        }

        private static double GetShrinkCapacity(ColumnSnapshot column)
            => ClampToZero(column.BaselineActualWidth - column.MinWidth);

        private static void ExpandNearestFirst(
            IReadOnlyList<ColumnSnapshot> columns,
            double magnitude,
            Dictionary<ColumnDefinition, double> targetWidths,
            bool nearestColumnIsLast)
        {
            var remainingMagnitude = magnitude;
            var increment = nearestColumnIsLast ? -1 : 1;
            for (var index = nearestColumnIsLast ? columns.Count - 1 : 0;
                 index >= 0 && index < columns.Count;
                 index += increment)
            {
                if (remainingMagnitude <= 0d)
                {
                    break;
                }

                var column = columns[index];
                var expansion = Math.Min(remainingMagnitude, GetExpansionCapacity(column));
                targetWidths[column.ColumnDefinition] = column.BaselineActualWidth + expansion;
                remainingMagnitude -= expansion;
            }
        }

        private static void ShrinkNearestFirst(
            IReadOnlyList<ColumnSnapshot> columns,
            double magnitude,
            Dictionary<ColumnDefinition, double> targetWidths,
            bool nearestColumnIsLast)
        {
            var remainingMagnitude = magnitude;
            var increment = nearestColumnIsLast ? -1 : 1;
            for (var index = nearestColumnIsLast ? columns.Count - 1 : 0;
                 index >= 0 && index < columns.Count;
                 index += increment)
            {
                if (remainingMagnitude <= 0d)
                {
                    break;
                }

                var column = columns[index];
                var shrinkage = Math.Min(remainingMagnitude, GetShrinkCapacity(column));
                targetWidths[column.ColumnDefinition] = column.BaselineActualWidth - shrinkage;
                remainingMagnitude -= shrinkage;
            }
        }

        private static double ClampToZero(double value) => value < 0d ? 0d : value;
    }

    private sealed class ColumnSnapshot
    {
        private ColumnSnapshot(ColumnDefinition columnDefinition, int physicalIndex, double baselineActualWidth, GridUnitType unitType, double minWidth, double maxWidth)
        {
            ColumnDefinition = columnDefinition;
            PhysicalIndex = physicalIndex;
            BaselineActualWidth = baselineActualWidth;
            UnitType = unitType;
            MinWidth = minWidth;
            MaxWidth = maxWidth;
        }

        public ColumnDefinition ColumnDefinition { get; }

        public int PhysicalIndex { get; }

        public double BaselineActualWidth { get; }

        public GridUnitType UnitType { get; }

        public double MinWidth { get; }

        public double MaxWidth { get; }

        public static bool TryCreate(ColumnDefinition columnDefinition, int physicalIndex, out ColumnSnapshot snapshot)
        {
            snapshot = null!;
            if (columnDefinition.Width.GridUnitType == GridUnitType.Auto)
            {
                return false;
            }

            var actualWidth = columnDefinition.ActualWidth;
            if (!double.IsFinite(actualWidth) || actualWidth <= 0d)
            {
                return false;
            }

            snapshot = new ColumnSnapshot(
                columnDefinition,
                physicalIndex,
                actualWidth,
                columnDefinition.Width.GridUnitType,
                columnDefinition.MinWidth,
                columnDefinition.MaxWidth);
            return true;
        }
    }
}
