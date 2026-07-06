// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WslContainersDesktop_App.ViewModels;

namespace WslContainersDesktop_App.Pages;

/// <summary>
/// ダッシュボード（概要）ページ。各リソースのサマリ件数と稼働中コンテナのリソース使用量を表示する。
/// </summary>
public sealed partial class DashboardPage : Page
{
    /// <summary>
    /// <see cref="DashboardPage"/> の新しいインスタンスを初期化する。
    /// </summary>
    public DashboardPage()
    {
        ViewModel = ((App)Application.Current).Services.GetRequiredService<DashboardViewModel>();

        InitializeComponent();

        Loaded += DashboardPage_Loaded;
    }

    /// <summary>
    /// ページのViewModel。
    /// </summary>
    public DashboardViewModel ViewModel { get; }

    /// <summary>
    /// x:Bind用の件数フォーマット関数。未取得（null）の場合はプレースホルダーを表示する。
    /// </summary>
    /// <param name="count">表示対象の件数。取得失敗時は <see langword="null"/>。</param>
    /// <returns>表示文字列。</returns>
    public string FormatCount(int? count) =>
        count?.ToString(CultureInfo.CurrentCulture) ?? "—";

    /// <summary>
    /// x:Bind用のbool→Visibility変換関数。値がtrueのとき表示する。
    /// </summary>
    /// <param name="value">変換元の値。</param>
    /// <returns>対応する <see cref="Visibility"/>。</returns>
    public Visibility ToVisibleWhenTrue(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// x:Bind用のbool→Visibility変換関数。値がfalseのとき表示する。
    /// </summary>
    /// <param name="value">変換元の値。</param>
    /// <returns>対応する <see cref="Visibility"/>。</returns>
    public Visibility ToVisibleWhenFalse(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private async void BtnStatsDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: DashboardContainerStatsRowViewModel row })
        {
            await ViewModel.OpenContainerDetailsCommand.ExecuteAsync(row);
        }
    }

    private async void BtnStatsLogs_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: DashboardContainerStatsRowViewModel row })
        {
            await ViewModel.OpenContainerLogsCommand.ExecuteAsync(row);
        }
    }
}
