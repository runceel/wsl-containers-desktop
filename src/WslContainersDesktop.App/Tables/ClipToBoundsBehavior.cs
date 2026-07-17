using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace WslContainersDesktop_App.Tables;

/// <summary>
/// Applies a clipping rectangle to a <see cref="FrameworkElement"/> based on its bounds.
/// </summary>
public static class ClipToBoundsBehavior
{
    /// <summary>
    /// Identifies the <c>IsEnabled</c> attached dependency property.
    /// </summary>
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(ClipToBoundsBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    /// <summary>
    /// Sets a value indicating whether clip-to-bounds behavior is enabled for the specified element.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="isEnabled"><see langword="true"/> to enable the behavior; otherwise, <see langword="false"/>.</param>
    public static void SetIsEnabled(FrameworkElement element, bool isEnabled)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(IsEnabledProperty, isEnabled);
    }

    /// <summary>
    /// Gets a value indicating whether clip-to-bounds behavior is enabled for the specified element.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns><see langword="true"/> when clip-to-bounds behavior is enabled; otherwise, <see langword="false"/>.</returns>
    public static bool GetIsEnabled(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (bool)element.GetValue(IsEnabledProperty);
    }

    /// <summary>
    /// Applies a clip rectangle to the specified element using the supplied size.
    /// </summary>
    /// <param name="element">The element whose clip should be updated.</param>
    /// <param name="size">The bounds to clip to.</param>
    internal static void ApplyClip(FrameworkElement element, Size size)
    {
        ArgumentNullException.ThrowIfNull(element);

        element.Clip = new RectangleGeometry
        {
            Rect = new Rect(0, 0, size.Width, size.Height),
        };
    }

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        element.SizeChanged -= OnSizeChanged;

        if ((bool)eventArgs.NewValue)
        {
            element.SizeChanged += OnSizeChanged;
            ApplyClip(element, new Size(element.ActualWidth, element.ActualHeight));
            return;
        }

        element.Clip = null;
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs eventArgs)
    {
        if (sender is FrameworkElement element)
        {
            ApplyClip(element, eventArgs.NewSize);
        }
    }
}
