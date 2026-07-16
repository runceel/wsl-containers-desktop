using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WslContainersDesktop_App.Tables;

/// <summary>
/// Identifies the role that a grid participates in for a table layout.
/// </summary>
public enum TableLayoutRole
{
    /// <summary>
    /// The grid represents the table header.
    /// </summary>
    Header,

    /// <summary>
    /// The grid represents a table row.
    /// </summary>
    Row,
}

/// <summary>
/// Applies a table column layout to a <see cref="Grid"/> and its <see cref="ColumnDefinition"/> instances.
/// </summary>
public static class TableLayoutBehavior
{
    private static readonly ConditionalWeakTable<Grid, AttachmentState> AttachmentStates = new();
    private static readonly ConditionalWeakTable<Grid, GridLifecycleState> GridLifecycleStates = new();

    /// <summary>
    /// Identifies the <c>Layout</c> attached dependency property.
    /// </summary>
    public static readonly DependencyProperty LayoutProperty =
        DependencyProperty.RegisterAttached(
            "Layout",
            typeof(TableColumnLayout),
            typeof(TableLayoutBehavior),
            new PropertyMetadata(null, OnLayoutChanged));

    /// <summary>
    /// Identifies the <c>Role</c> attached dependency property.
    /// </summary>
    public static readonly DependencyProperty RoleProperty =
        DependencyProperty.RegisterAttached(
            "Role",
            typeof(TableLayoutRole),
            typeof(TableLayoutBehavior),
            new PropertyMetadata(TableLayoutRole.Header, OnRoleChanged));

    /// <summary>
    /// Identifies the <c>ColumnIndex</c> attached dependency property.
    /// </summary>
    public static readonly DependencyProperty ColumnIndexProperty =
        DependencyProperty.RegisterAttached(
            "ColumnIndex",
            typeof(int),
            typeof(TableLayoutBehavior),
            new PropertyMetadata(-1));

    /// <summary>
    /// Sets the layout associated with the specified grid.
    /// </summary>
    /// <param name="grid">The grid.</param>
    /// <param name="layout">The layout to attach, or <see langword="null"/> to clear it.</param>
    public static void SetLayout(Grid grid, TableColumnLayout? layout)
    {
        ArgumentNullException.ThrowIfNull(grid);
        grid.SetValue(LayoutProperty, layout);
    }

    /// <summary>
    /// Gets the layout associated with the specified grid.
    /// </summary>
    /// <param name="grid">The grid.</param>
    /// <returns>The attached layout, if one exists.</returns>
    public static TableColumnLayout? GetLayout(Grid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return (TableColumnLayout?)grid.GetValue(LayoutProperty);
    }

    /// <summary>
    /// Sets the role associated with the specified grid.
    /// </summary>
    /// <param name="grid">The grid.</param>
    /// <param name="role">The role.</param>
    public static void SetRole(Grid grid, TableLayoutRole role)
    {
        ArgumentNullException.ThrowIfNull(grid);
        grid.SetValue(RoleProperty, role);
    }

    /// <summary>
    /// Gets the role associated with the specified grid.
    /// </summary>
    /// <param name="grid">The grid.</param>
    /// <returns>The attached role.</returns>
    public static TableLayoutRole GetRole(Grid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return (TableLayoutRole)grid.GetValue(RoleProperty);
    }

    /// <summary>
    /// Sets the logical column index associated with the specified dependency object.
    /// </summary>
    /// <param name="dependencyObject">The dependency object.</param>
    /// <param name="columnIndex">The logical column index.</param>
    public static void SetColumnIndex(DependencyObject dependencyObject, int columnIndex)
    {
        ArgumentNullException.ThrowIfNull(dependencyObject);
        dependencyObject.SetValue(ColumnIndexProperty, columnIndex);
    }

    /// <summary>
    /// Gets the logical column index associated with the specified dependency object.
    /// </summary>
    /// <param name="dependencyObject">The dependency object.</param>
    /// <returns>The logical column index, or <c>-1</c> when the object is unmarked.</returns>
    public static int GetColumnIndex(DependencyObject dependencyObject)
    {
        ArgumentNullException.ThrowIfNull(dependencyObject);
        return (int)dependencyObject.GetValue(ColumnIndexProperty);
    }

    /// <summary>
    /// Attaches the current layout to the specified grid by applying it immediately.
    /// </summary>
    /// <param name="grid">The grid.</param>
    internal static void Attach(Grid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        DetachCore(grid);

        var layout = GetLayout(grid);
        if (layout is null)
        {
            throw new InvalidOperationException("The specified grid does not have a layout attached.");
        }

        AttachCore(grid, layout);
    }

    internal static void HandleLoaded(Grid? grid)
    {
        if (grid is null)
        {
            return;
        }

        if (AttachmentStates.TryGetValue(grid, out _))
        {
            return;
        }

        if (GetLayout(grid) is not { } layout)
        {
            return;
        }

        AttachCore(grid, layout);
    }

    internal static void HandleUnloaded(Grid? grid)
    {
        if (grid is null)
        {
            return;
        }

        DetachCore(grid);
    }

    /// <summary>
    /// Applies the current layout to the specified grid.
    /// </summary>
    /// <param name="grid">The grid.</param>
    internal static void Apply(Grid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (AttachmentStates.TryGetValue(grid, out var state))
        {
            state.ApplyLayout();
            return;
        }

        var layout = GetLayout(grid);
        if (layout is null)
        {
            throw new InvalidOperationException("The specified grid does not have a layout attached.");
        }

        ApplyLayout(grid, layout);
    }

    /// <summary>
    /// Detaches the current layout from the specified grid.
    /// </summary>
    /// <param name="grid">The grid.</param>
    internal static void Detach(Grid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        DetachCore(grid);
    }

    private static void AttachCore(Grid grid, TableColumnLayout layout)
    {
        var state = new AttachmentState(grid, layout, GetRole(grid));
        try
        {
            state.Attach();
            AttachmentStates.Add(grid, state);
        }
        catch
        {
            state.Detach();
            throw;
        }
    }

    private static bool DetachCore(Grid grid)
    {
        if (!AttachmentStates.TryGetValue(grid, out var state))
        {
            return false;
        }

        AttachmentStates.Remove(grid);
        state.Detach();
        return true;
    }

    private static void ApplyLayout(Grid grid, TableColumnLayout layout)
    {
        foreach (var column in grid.ColumnDefinitions)
        {
            var logicalIndex = GetColumnIndex(column);
            if (logicalIndex < 0 || logicalIndex >= layout.ColumnCount)
            {
                continue;
            }

            ApplyColumn(column, layout, logicalIndex);
        }
    }

    private static void OnLayoutChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not Grid grid)
        {
            return;
        }

        Reconfigure(grid, ReconfigurationSource.Layout);
    }

    private static void OnRoleChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not Grid grid)
        {
            return;
        }

        Reconfigure(grid, ReconfigurationSource.Role);
    }

    private static void Reconfigure(Grid grid, ReconfigurationSource source)
    {
        var layoutChanged = source == ReconfigurationSource.Layout;
        var wasAttached = DetachCore(grid);
        var layout = GetLayout(grid);

        if (layoutChanged)
        {
            SetLifecycleEventHandlers(grid, shouldRegister: layout is not null);
        }

        if (layout is null)
        {
            return;
        }

        if (wasAttached || (layoutChanged && grid.IsLoaded))
        {
            AttachCore(grid, layout);
        }
        else if (layoutChanged)
        {
            ApplyLayout(grid, layout);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Grid grid)
        {
            HandleLoaded(grid);
        }
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Grid grid)
        {
            HandleUnloaded(grid);
        }
    }

    private static void SetLifecycleEventHandlers(Grid grid, bool shouldRegister)
    {
        GridLifecycleState? state;
        if (shouldRegister)
        {
            state = GridLifecycleStates.GetValue(grid, _ => new GridLifecycleState());
        }
        else if (!GridLifecycleStates.TryGetValue(grid, out state))
        {
            return;
        }

        if (state is null || state.HasLoadedHandlers == shouldRegister)
        {
            return;
        }

        if (shouldRegister)
        {
            grid.Loaded += OnLoaded;
            grid.Unloaded += OnUnloaded;
        }
        else
        {
            grid.Loaded -= OnLoaded;
            grid.Unloaded -= OnUnloaded;
        }

        state.HasLoadedHandlers = shouldRegister;
    }

    private sealed class GridLifecycleState
    {
        public bool HasLoadedHandlers { get; set; }
    }

    private enum ReconfigurationSource
    {
        Layout,
        Role,
    }

    private static void ApplyColumn(ColumnDefinition column, TableColumnLayout layout, int logicalIndex)
    {
        column.MinWidth = layout.GetMinWidth(logicalIndex);
        column.MaxWidth = layout.GetMaxWidth(logicalIndex);
        column.Width = layout.GetWidth(logicalIndex);
    }

    private sealed class AttachmentState(Grid grid, TableColumnLayout layout, TableLayoutRole role)
    {
        private readonly List<CallbackRegistration> _callbackRegistrations = [];
        private readonly Dictionary<ColumnDefinition, int> _headerColumnIndices = [];
        private Grid? _grid = grid;
        private TableColumnLayout? _layout = layout;
        private bool _isApplyingLayout;
        private bool _isDetached;

        private TableLayoutRole Role { get; } = role;

        public void Attach()
        {
            ApplyLayout();
            RegisterLayoutWidthCallbacks();

            if (Role == TableLayoutRole.Header)
            {
                RegisterHeaderWidthCallbacks();
            }
        }

        public void ApplyLayout()
        {
            if (_isApplyingLayout || _isDetached || _grid is not { } grid || _layout is not { } layout)
            {
                return;
            }

            _isApplyingLayout = true;
            try
            {
                TableLayoutBehavior.ApplyLayout(grid, layout);
            }
            finally
            {
                _isApplyingLayout = false;
            }
        }

        public void Detach()
        {
            if (_isDetached)
            {
                return;
            }

            _isDetached = true;
            try
            {
                for (var index = _callbackRegistrations.Count - 1; index >= 0; index--)
                {
                    _callbackRegistrations[index].Unregister();
                }
            }
            finally
            {
                _callbackRegistrations.Clear();
                _headerColumnIndices.Clear();
                _grid = null;
                _layout = null;
                _isApplyingLayout = false;
            }
        }

        private void RegisterLayoutWidthCallbacks()
        {
            if (_layout is not { } layout)
            {
                return;
            }

            for (var index = 0; index < layout.ColumnCount; index++)
            {
                RegisterCallback(layout, layout.GetWidthProperty(index), OnLayoutWidthChanged);
            }
        }

        private void RegisterHeaderWidthCallbacks()
        {
            if (_grid is not { } grid || _layout is not { } layout)
            {
                return;
            }

            foreach (var column in grid.ColumnDefinitions)
            {
                var logicalIndex = GetColumnIndex(column);
                if (logicalIndex < 0 || logicalIndex >= layout.ColumnCount)
                {
                    continue;
                }

                _headerColumnIndices.Add(column, logicalIndex);
                RegisterCallback(column, ColumnDefinition.WidthProperty, OnHeaderWidthChanged);
            }
        }

        private void RegisterCallback(
            DependencyObject source,
            DependencyProperty property,
            DependencyPropertyChangedCallback callback)
        {
            var token = source.RegisterPropertyChangedCallback(property, callback);
            try
            {
                _callbackRegistrations.Add(new CallbackRegistration(source, property, token));
            }
            catch
            {
                source.UnregisterPropertyChangedCallback(property, token);
                throw;
            }
        }

        private void OnLayoutWidthChanged(DependencyObject sender, DependencyProperty property)
        {
            ApplyLayout();
        }

        private void OnHeaderWidthChanged(DependencyObject sender, DependencyProperty property)
        {
            if (_isApplyingLayout
                || _isDetached
                || _layout is not { } layout
                || sender is not ColumnDefinition column
                || !_headerColumnIndices.TryGetValue(column, out var logicalIndex)
                || column.Width.GridUnitType == GridUnitType.Auto)
            {
                return;
            }

            layout.SetWidth(logicalIndex, column.Width);
        }

        private sealed class CallbackRegistration(
            DependencyObject source,
            DependencyProperty property,
            long token)
        {
            public void Unregister()
            {
                source.UnregisterPropertyChangedCallback(property, token);
            }
        }
    }
}
