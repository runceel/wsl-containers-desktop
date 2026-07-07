// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WslContainersDesktop_App.ViewModels;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Resources;

namespace WslContainersDesktop_App.Pages;

/// <summary>
/// 設定ページ（WSL連携状態の確認とリソース制限の編集）。
/// </summary>
public sealed partial class SettingsPage : Page
{
    private readonly ResourceLoader _resourceLoader = new();

    /// <summary>
    /// <see cref="SettingsPage"/> の新しいインスタンスを初期化する。
    /// </summary>
    public SettingsPage()
    {
        ViewModel = ((App)Application.Current).Services.GetRequiredService<SettingsViewModel>();

        InitializeComponent();

        Loaded += SettingsPage_Loaded;
    }

    /// <summary>
    /// ページのViewModel。
    /// </summary>
    public SettingsViewModel ViewModel { get; }

    /// <summary>
    /// 表示用のアプリパッケージバージョン文字列。
    /// </summary>
    public string AppVersionText { get; } = FormatPackageVersion(
        Package.Current.Id.Version.Major,
        Package.Current.Id.Version.Minor,
        Package.Current.Id.Version.Build,
        Package.Current.Id.Version.Revision);

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private async void BtnResetResourceLimits_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _resourceLoader.GetString("ResetResourceLimitsConfirmationDialog_Title"),
            Content = _resourceLoader.GetString("ResetResourceLimitsConfirmationDialog_Message"),
            PrimaryButtonText = _resourceLoader.GetString("ResetResourceLimitsConfirmationDialog_PrimaryButtonText"),
            CloseButtonText = _resourceLoader.GetString("ResetResourceLimitsConfirmationDialog_CloseButtonText"),
            DefaultButton = ContentDialogButton.Close,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.ResetCommand.ExecuteAsync(null);
        }
    }

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

    /// <summary>
    /// パッケージバージョンの4要素を表示用文字列に変換する。
    /// </summary>
    /// <param name="major">メジャーバージョン。</param>
    /// <param name="minor">マイナーバージョン。</param>
    /// <param name="build">ビルド番号。</param>
    /// <param name="revision">リビジョン番号。</param>
    /// <returns>表示用の4要素バージョン文字列。</returns>
    public static string FormatPackageVersion(int major, int minor, int build, int revision)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}.{3}", major, minor, build, revision);
    }
}
