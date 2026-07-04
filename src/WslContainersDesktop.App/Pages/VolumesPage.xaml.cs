// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WslContainersDesktop_App.ViewModels;
using Windows.ApplicationModel.Resources;

namespace WslContainersDesktop_App.Pages;

/// <summary>
/// コンテナーボリューム一覧ページ。
/// </summary>
public sealed partial class VolumesPage : Page
{
    private readonly ResourceLoader _resourceLoader = new();

    /// <summary>
    /// <see cref="VolumesPage"/> の新しいインスタンスを初期化する。
    /// </summary>
    public VolumesPage()
    {
        ViewModel = ((App)Application.Current).Services.GetRequiredService<VolumesViewModel>();

        InitializeComponent();

        Loaded += VolumesPage_Loaded;
    }

    /// <summary>
    /// ページのViewModel。
    /// </summary>
    public VolumesViewModel ViewModel { get; }

    private async void VolumesPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private async void BtnDeleteVolume_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: VolumeRowViewModel row })
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _resourceLoader.GetString("DeleteVolumeConfirmationDialog_Title"),
            Content = string.Format(_resourceLoader.GetString("DeleteVolumeConfirmationDialog_Message"), row.Name),
            PrimaryButtonText = _resourceLoader.GetString("DeleteVolumeConfirmationDialog_PrimaryButtonText"),
            CloseButtonText = _resourceLoader.GetString("DeleteVolumeConfirmationDialog_CloseButtonText"),
            DefaultButton = ContentDialogButton.Close,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.DeleteCommand.ExecuteAsync(row);
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
}
