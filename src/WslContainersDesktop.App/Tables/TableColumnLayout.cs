using System;
using Microsoft.UI.Xaml;

namespace WslContainersDesktop_App.Tables;

/// <summary>
/// Represents a layout definition for a table's column widths.
/// </summary>
public sealed class TableColumnLayout : DependencyObject
{
    private static readonly DependencyProperty[] WidthProperties =
    [
        RegisterWidthProperty("Width0Property"),
        RegisterWidthProperty("Width1Property"),
        RegisterWidthProperty("Width2Property"),
        RegisterWidthProperty("Width3Property"),
        RegisterWidthProperty("Width4Property"),
        RegisterWidthProperty("Width5Property"),
    ];

    private readonly double[] _minWidths;
    private readonly double[] _maxWidths;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableColumnLayout"/> class.
    /// </summary>
    public TableColumnLayout()
        : this(1, [new GridLength(1d, GridUnitType.Star)], [0d], [double.PositiveInfinity], 0d)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TableColumnLayout"/> class with the specified values.
    /// </summary>
    /// <param name="columnCount">The number of columns in the layout.</param>
    /// <param name="widths">The widths assigned to each column.</param>
    /// <param name="minWidths">The minimum widths assigned to each column.</param>
    /// <param name="maxWidths">The maximum widths assigned to each column.</param>
    /// <param name="actionRailWidth">The width of the action rail.</param>
    public TableColumnLayout(int columnCount, GridLength[] widths, double[] minWidths, double[] maxWidths, double actionRailWidth)
    {
        if (columnCount is < 1 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(columnCount));
        }

        ArgumentNullException.ThrowIfNull(widths);
        ArgumentNullException.ThrowIfNull(minWidths);
        ArgumentNullException.ThrowIfNull(maxWidths);

        if (widths.Length != columnCount || minWidths.Length != columnCount || maxWidths.Length != columnCount)
        {
            throw new ArgumentException("The width/min/max collections must match the column count.", nameof(columnCount));
        }

        for (var index = 0; index < columnCount; index++)
        {
            if (double.IsNaN(minWidths[index]) || minWidths[index] < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(minWidths));
            }

            if (double.IsNaN(maxWidths[index]) || maxWidths[index] < minWidths[index])
            {
                throw new ArgumentOutOfRangeException(nameof(maxWidths));
            }
        }

        if (actionRailWidth < 0d || double.IsNaN(actionRailWidth) || double.IsInfinity(actionRailWidth))
        {
            throw new ArgumentOutOfRangeException(nameof(actionRailWidth));
        }

        ColumnCount = columnCount;
        ActionRailWidth = actionRailWidth;
        _minWidths = [.. minWidths];
        _maxWidths = [.. maxWidths];

        for (var index = 0; index < columnCount; index++)
        {
            SetValue(GetWidthProperty(index), widths[index]);
        }
    }

    /// <summary>
    /// Gets the number of columns in the layout.
    /// </summary>
    public int ColumnCount { get; }

    /// <summary>
    /// Gets the width of the action rail.
    /// </summary>
    public double ActionRailWidth { get; }

    /// <summary>
    /// Gets the width for the specified column index.
    /// </summary>
    /// <param name="index">The zero-based column index.</param>
    /// <returns>The width definition for the specified column.</returns>
    public GridLength GetWidth(int index)
    {
        ValidateIndex(index);
        return (GridLength)GetValue(GetWidthProperty(index));
    }

    /// <summary>
    /// Gets the minimum width for the specified column index.
    /// </summary>
    /// <param name="index">The zero-based column index.</param>
    /// <returns>The minimum width for the specified column.</returns>
    public double GetMinWidth(int index)
    {
        ValidateIndex(index);
        return _minWidths[index];
    }

    /// <summary>
    /// Gets the maximum width for the specified column index.
    /// </summary>
    /// <param name="index">The zero-based column index.</param>
    /// <returns>The maximum width for the specified column.</returns>
    public double GetMaxWidth(int index)
    {
        ValidateIndex(index);
        return _maxWidths[index];
    }

    /// <summary>
    /// Sets the width for the specified column index.
    /// </summary>
    /// <param name="index">The zero-based column index.</param>
    /// <param name="width">The new width definition.</param>
    public void SetWidth(int index, GridLength width)
    {
        ValidateIndex(index);

        if (width.GridUnitType == GridUnitType.Auto)
        {
            throw new ArgumentException("Auto widths are not supported.", nameof(width));
        }

        if (width.GridUnitType == GridUnitType.Pixel)
        {
            var clampedValue = Math.Clamp(width.Value, _minWidths[index], _maxWidths[index]);
            SetValue(GetWidthProperty(index), new GridLength(clampedValue, GridUnitType.Pixel));
            return;
        }

        SetValue(GetWidthProperty(index), width);
    }

    internal DependencyProperty GetWidthProperty(int index)
    {
        ValidateIndex(index);
        return WidthProperties[index];
    }

    private static DependencyProperty RegisterWidthProperty(string name) =>
        DependencyProperty.Register(
            name,
            typeof(GridLength),
            typeof(TableColumnLayout),
            new PropertyMetadata(default(GridLength)));

    private void ValidateIndex(int index)
    {
        if (index < 0 || index >= ColumnCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
