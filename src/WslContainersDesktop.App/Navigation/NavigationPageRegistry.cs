// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using WslContainersDesktop_App.Pages;

namespace WslContainersDesktop_App.Navigation;

/// <summary>
/// <see cref="NavigationPageKey"/> と、対応するページの <see cref="Type"/> を関連付けるレジストリ。
/// </summary>
public static class NavigationPageRegistry
{
    /// <summary>
    /// 指定した <see cref="NavigationPageKey"/> に対応するページの <see cref="Type"/> を取得する。
    /// </summary>
    /// <param name="pageKey">取得対象のページキー。</param>
    /// <returns>対応するページの <see cref="Type"/>。</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="pageKey"/> が定義されていない値の場合にスローされる。
    /// </exception>
    public static Type GetPageType(NavigationPageKey pageKey)
    {
        return pageKey.EnsureDefined(nameof(pageKey)) switch
        {
            NavigationPageKey.Dashboard => typeof(DashboardPage),
            NavigationPageKey.Containers => typeof(ContainersPage),
            NavigationPageKey.Images => typeof(ImagesPage),
            NavigationPageKey.Volumes => typeof(VolumesPage),
            NavigationPageKey.Networks => typeof(NetworksPage),
            NavigationPageKey.Settings => typeof(SettingsPage),
            _ => throw new ArgumentOutOfRangeException(nameof(pageKey), pageKey, "未定義のNavigationPageKeyです。"),
        };
    }
}
