// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WslContainersDesktop_App.ViewModels;
using Windows.ApplicationModel.Resources;

namespace WslContainersDesktop_App.Pages;

/// <summary>
/// コンテナーイメージ一覧ページ。
/// </summary>
public sealed partial class ImagesPage : Page
{
    private readonly ResourceLoader _resourceLoader = new();

    /// <summary>
    /// <see cref="ImagesPage"/> の新しいインスタンスを初期化する。
    /// </summary>
    public ImagesPage()
    {
        ViewModel = ((App)Application.Current).Services.GetRequiredService<ImagesViewModel>();

        InitializeComponent();

        Loaded += ImagesPage_Loaded;
    }

    /// <summary>
    /// ページのViewModel。
    /// </summary>
    public ImagesViewModel ViewModel { get; }

    private async void ImagesPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private async void BtnDeleteImage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: ImageRowViewModel row })
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _resourceLoader.GetString("DeleteImageConfirmationDialog_Title"),
            Content = string.Format(_resourceLoader.GetString("DeleteImageConfirmationDialog_Message"), row.DisplayName),
            PrimaryButtonText = _resourceLoader.GetString("DeleteImageConfirmationDialog_PrimaryButtonText"),
            CloseButtonText = _resourceLoader.GetString("DeleteImageConfirmationDialog_CloseButtonText"),
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

    /// <summary>
    /// x:Bind用のnull/空判定関数。
    /// </summary>
    /// <param name="value">判定対象の値。</param>
    /// <returns>値がnullでも空でもない場合は <see langword="true"/>。</returns>
    public bool IsNotNullOrEmpty(string? value) => !string.IsNullOrEmpty(value);
}
