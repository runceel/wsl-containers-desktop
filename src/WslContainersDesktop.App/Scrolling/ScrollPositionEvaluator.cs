// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace WslContainersDesktop_App.Scrolling;

/// <summary>
/// ScrollViewerの計測値から「末尾までスクロールされているか」を判定する純粋ロジック。
/// WinUIの型に依存しないため、実際のScrollViewerを介さず単体テストできる。
/// </summary>
public static class ScrollPositionEvaluator
{
    /// <summary>
    /// 指定したスクロール位置が末尾（コンテンツの一番下）とみなせるかどうかを判定する。
    /// </summary>
    /// <param name="verticalOffset">現在の垂直スクロールオフセット。</param>
    /// <param name="viewportHeight">表示領域（ビューポート）の高さ。</param>
    /// <param name="extentHeight">スクロール可能なコンテンツ全体の高さ。</param>
    /// <param name="tolerance">
    /// 末尾とみなす許容誤差（ピクセル）。ScrollViewerの計測値は端数を含むため、
    /// 完全に一致しなくても末尾として扱うための許容幅。既定値は2.0。
    /// </param>
    /// <returns>末尾までスクロールされているとみなせる場合は <see langword="true"/>。</returns>
    public static bool IsAtBottom(double verticalOffset, double viewportHeight, double extentHeight, double tolerance = 2.0)
    {
        if (extentHeight <= viewportHeight)
        {
            // コンテンツがビューポートに収まりスクロールできない場合は常に末尾とみなす。
            return true;
        }

        return extentHeight - (verticalOffset + viewportHeight) <= tolerance;
    }
}
