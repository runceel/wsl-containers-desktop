// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace WslContainersDesktop_App.Navigation;

/// <summary>
/// <see cref="NavigationPageKey"/> に対する検証用の拡張メソッドを提供する。
/// </summary>
internal static class NavigationPageKeyExtensions
{
    /// <summary>
    /// <paramref name="pageKey"/> が <see cref="NavigationPageKey"/> に定義された値であることを検証する。
    /// </summary>
    /// <param name="pageKey">検証対象のページキー。</param>
    /// <param name="paramName">検証に失敗した場合に例外へ渡す呼び出し元のパラメーター名。</param>
    /// <returns>検証済みの <paramref name="pageKey"/>。</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="pageKey"/> が定義されていない値の場合にスローされる。
    /// </exception>
    public static NavigationPageKey EnsureDefined(this NavigationPageKey pageKey, string paramName)
    {
        if (!Enum.IsDefined(pageKey))
        {
            throw new ArgumentOutOfRangeException(paramName, pageKey, "未定義のNavigationPageKeyです。");
        }

        return pageKey;
    }
}
