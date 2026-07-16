using System;
using System.Collections.Generic;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WslContainersDesktop_App.Tables;

/// <summary>
/// Represents a splitter that redistributes width between a left table column and the trailing data columns to its right.
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
        private readonly ColumnSnapshot _leftColumn;
        private readonly List<ColumnSnapshot> _trailingColumns;

        private ResizeSession(
            List<ColumnSnapshot> dataColumns,
            ColumnSnapshot leftColumn,
            List<ColumnSnapshot> trailingColumns)
        {
            _dataColumns = dataColumns;
            _leftColumn = leftColumn;
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

            ColumnSnapshot? leftColumn = null;
            var trailingColumns = new List<ColumnSnapshot>();
            foreach (var dataColumn in dataColumns)
            {
                if (dataColumn.PhysicalIndex < splitterPhysicalIndex)
                {
                    leftColumn = dataColumn;
                }
                else if (dataColumn.PhysicalIndex > splitterPhysicalIndex)
                {
                    trailingColumns.Add(dataColumn);
                }
            }

            if (leftColumn is null || trailingColumns.Count == 0)
            {
                return null;
            }

            return new ResizeSession(dataColumns, leftColumn, trailingColumns);
        }

        public bool Apply(double horizontalChange)
        {
            var targetWidths = new Dictionary<ColumnDefinition, double>();
            foreach (var column in _dataColumns)
            {
                targetWidths[column.ColumnDefinition] = column.BaselineActualWidth;
            }

            double effectiveDelta;
            if (horizontalChange > 0d)
            {
                var leftCapacity = ClampToZero(_leftColumn.MaxWidth - _leftColumn.BaselineActualWidth);
                var trailingCapacity = 0d;
                foreach (var trailingColumn in _trailingColumns)
                {
                    trailingCapacity += ClampToZero(trailingColumn.BaselineActualWidth - trailingColumn.MinWidth);
                }

                effectiveDelta = Math.Min(horizontalChange, leftCapacity);
                effectiveDelta = Math.Min(effectiveDelta, trailingCapacity);
                if (effectiveDelta <= 0d)
                {
                    return false;
                }

                var remainingDelta = effectiveDelta;
                foreach (var trailingColumn in _trailingColumns)
                {
                    if (remainingDelta <= 0d)
                    {
                        break;
                    }

                    var shrinkAmount = Math.Min(remainingDelta, ClampToZero(trailingColumn.BaselineActualWidth - trailingColumn.MinWidth));
                    targetWidths[trailingColumn.ColumnDefinition] = trailingColumn.BaselineActualWidth - shrinkAmount;
                    remainingDelta -= shrinkAmount;
                }

                targetWidths[_leftColumn.ColumnDefinition] = _leftColumn.BaselineActualWidth + effectiveDelta;
            }
            else
            {
                var requestedMagnitude = Math.Abs(horizontalChange);
                var leftCapacity = ClampToZero(_leftColumn.BaselineActualWidth - _leftColumn.MinWidth);
                var trailingCapacity = 0d;
                foreach (var trailingColumn in _trailingColumns)
                {
                    trailingCapacity += ClampToZero(trailingColumn.MaxWidth - trailingColumn.BaselineActualWidth);
                }

                var effectiveMagnitude = Math.Min(requestedMagnitude, leftCapacity);
                effectiveMagnitude = Math.Min(effectiveMagnitude, trailingCapacity);
                if (effectiveMagnitude <= 0d)
                {
                    return false;
                }

                var remainingMagnitude = effectiveMagnitude;
                foreach (var trailingColumn in _trailingColumns)
                {
                    if (remainingMagnitude <= 0d)
                    {
                        break;
                    }

                    var expandAmount = Math.Min(remainingMagnitude, ClampToZero(trailingColumn.MaxWidth - trailingColumn.BaselineActualWidth));
                    targetWidths[trailingColumn.ColumnDefinition] = trailingColumn.BaselineActualWidth + expandAmount;
                    remainingMagnitude -= expandAmount;
                }

                targetWidths[_leftColumn.ColumnDefinition] = _leftColumn.BaselineActualWidth - effectiveMagnitude;
            }

            var starColumns = new List<ColumnSnapshot>();
            foreach (var column in _dataColumns)
            {
                if (column.UnitType == GridUnitType.Star)
                {
                    starColumns.Add(column);
                }
            }

            foreach (var trailingColumn in _trailingColumns)
            {
                if (trailingColumn.UnitType == GridUnitType.Pixel)
                {
                    trailingColumn.ColumnDefinition.Width = new GridLength(targetWidths[trailingColumn.ColumnDefinition], GridUnitType.Pixel);
                }
            }

            if (_leftColumn.UnitType == GridUnitType.Pixel)
            {
                _leftColumn.ColumnDefinition.Width = new GridLength(targetWidths[_leftColumn.ColumnDefinition], GridUnitType.Pixel);
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
