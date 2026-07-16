using System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using Windows.Foundation;
using WslContainersDesktop_App.Tables;

namespace WslContainersDesktop_App_Tests.Tables;

[TestClass]
[DoNotParallelize]
public class ClipToBoundsBehaviorTests
{
    [UITestMethod]
    public void ApplyClip_ExplicitSize_SetsRectangleGeometryToExactBounds()
    {
        // Arrange
        var grid = new Grid { Width = 640, Height = 720 };

        // Act
        ClipToBoundsBehavior.ApplyClip(grid, new Size(320, 48));

        // Assert
        AssertRectangleBounds(grid.Clip, new Rect(0d, 0d, 320d, 48d));
    }

    [UITestMethod]
    public void ApplyClip_SecondSize_UpdatesBounds()
    {
        // Arrange
        var grid = new Grid();

        // Act
        ClipToBoundsBehavior.ApplyClip(grid, new Size(120, 24));
        ClipToBoundsBehavior.ApplyClip(grid, new Size(240, 56));

        // Assert
        AssertRectangleBounds(grid.Clip, new Rect(0d, 0d, 240d, 56d));
    }

    [UITestMethod]
    public void ApplyClip_ZeroSize_UsesEmptyBounds()
    {
        // Arrange
        var grid = new Grid();

        // Act
        ClipToBoundsBehavior.ApplyClip(grid, new Size(0, 0));

        // Assert
        AssertRectangleBounds(grid.Clip, new Rect(0d, 0d, 0d, 0d));
    }

    [UITestMethod]
    public void AttachedProperty_RoundTripsEnabledState()
    {
        // Arrange
        var grid = new Grid();

        // Act
        ClipToBoundsBehavior.SetIsEnabled(grid, true);

        // Assert
        Assert.IsTrue(ClipToBoundsBehavior.GetIsEnabled(grid));

        // Act
        ClipToBoundsBehavior.SetIsEnabled(grid, false);

        // Assert
        Assert.IsFalse(ClipToBoundsBehavior.GetIsEnabled(grid));
    }

    [UITestMethod]
    public void SetIsEnabled_FalseAfterEnabled_ClearsClip()
    {
        // Arrange
        var grid = new Grid();
        ClipToBoundsBehavior.SetIsEnabled(grid, true);
        ClipToBoundsBehavior.ApplyClip(grid, new Size(320, 48));

        // Act
        ClipToBoundsBehavior.SetIsEnabled(grid, false);

        // Assert
        Assert.IsNull(grid.Clip);
    }

    [UITestMethod]
    public void ApplyClip_NullElement_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() => ClipToBoundsBehavior.ApplyClip(null!, new Size(320, 48)));
    }

    private static void AssertRectangleBounds(Geometry? geometry, Rect expectedBounds)
    {
        var rectangleGeometry = geometry as RectangleGeometry;
        Assert.IsNotNull(rectangleGeometry);
        Assert.AreEqual(expectedBounds.X, rectangleGeometry.Rect.X);
        Assert.AreEqual(expectedBounds.Y, rectangleGeometry.Rect.Y);
        Assert.AreEqual(expectedBounds.Width, rectangleGeometry.Rect.Width);
        Assert.AreEqual(expectedBounds.Height, rectangleGeometry.Rect.Height);
    }
}
