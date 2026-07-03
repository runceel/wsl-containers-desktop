using WslContainersDesktop_App.Scrolling;

namespace WslContainersDesktop_App_Tests.Scrolling;

[TestClass]
public sealed class ScrollPositionEvaluatorTests
{
    [TestMethod]
    public void IsAtBottom_OffsetPlusViewportEqualsExtent_ReturnsTrue()
    {
        // Arrange & Act
        var result = ScrollPositionEvaluator.IsAtBottom(verticalOffset: 800, viewportHeight: 200, extentHeight: 1000);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsAtBottom_OffsetPlusViewportWithinTolerance_ReturnsTrue()
    {
        // Arrange & Act
        // 1000 - (799 + 200) = 1.0 <= 既定のtolerance(2.0)。
        var result = ScrollPositionEvaluator.IsAtBottom(verticalOffset: 799, viewportHeight: 200, extentHeight: 1000);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsAtBottom_OffsetPlusViewportBeyondTolerance_ReturnsFalse()
    {
        // Arrange & Act
        // 1000 - (700 + 200) = 100.0 > 既定のtolerance(2.0)。
        var result = ScrollPositionEvaluator.IsAtBottom(verticalOffset: 700, viewportHeight: 200, extentHeight: 1000);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsAtBottom_GapExactlyEqualsTolerance_ReturnsTrue()
    {
        // Arrange & Act
        // 1000 - (798 + 200) = 2.0 == 既定のtolerance(2.0)。境界値は「末尾」とみなす。
        var result = ScrollPositionEvaluator.IsAtBottom(verticalOffset: 798, viewportHeight: 200, extentHeight: 1000);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsAtBottom_ExtentHeightNotGreaterThanViewportHeight_ReturnsTrue()
    {
        // Arrange & Act
        // コンテンツがビューポートに収まりスクロール不可能な場合は常に「末尾」とみなす。
        var result = ScrollPositionEvaluator.IsAtBottom(verticalOffset: 0, viewportHeight: 300, extentHeight: 200);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsAtBottom_CustomToleranceNarrowerThanGap_ReturnsFalse()
    {
        // Arrange & Act
        // 差分1.5だが、tolerance=1.0を明示指定した場合は「末尾ではない」と判定する。
        var result = ScrollPositionEvaluator.IsAtBottom(verticalOffset: 798.5, viewportHeight: 200, extentHeight: 1000, tolerance: 1.0);

        // Assert
        Assert.IsFalse(result);
    }
}
