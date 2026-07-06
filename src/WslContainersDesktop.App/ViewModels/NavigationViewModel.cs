// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WslContainersDesktop_App.Navigation;

namespace WslContainersDesktop_App.ViewModels;

/// <summary>
/// アプリケーションの現在表示中のページを管理するViewModel。
/// </summary>
public sealed partial class NavigationViewModel : ObservableObject
{
    [ObservableProperty]
    public partial NavigationPageKey CurrentPageKey { get; private set; }

    /// <summary>
    /// <see cref="NavigationViewModel"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="initialPageKey">初期表示するページのキー。</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="initialPageKey"/> が定義されていない値の場合にスローされる。
    /// </exception>
    public NavigationViewModel(NavigationPageKey initialPageKey = NavigationPageKey.Dashboard)
    {
        CurrentPageKey = initialPageKey.EnsureDefined(nameof(initialPageKey));
    }

    /// <summary>
    /// 指定したページキーへ遷移する。
    /// </summary>
    /// <param name="pageKey">遷移先のページキー。</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="pageKey"/> が定義されていない値の場合にスローされる。
    /// </exception>
    [RelayCommand]
    private void NavigateTo(NavigationPageKey pageKey)
    {
        CurrentPageKey = pageKey.EnsureDefined(nameof(pageKey));
    }
}
