// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WslContainersDesktop_App.ViewModels;
using Windows.ApplicationModel.Resources;

namespace WslContainersDesktop_App.Pages;

/// <summary>
/// コンテナーネットワーク一覧ページ。
/// </summary>
public sealed partial class NetworksPage : Page
{
    private readonly ResourceLoader _resourceLoader = new();

    /// <summary>
    /// <see cref="NetworksPage"/> の新しいインスタンスを初期化する。
    /// </summary>
    public NetworksPage()
    {
        ViewModel = ((App)Application.Current).Services.GetRequiredService<NetworksViewModel>();

        InitializeComponent();

        Loaded += NetworksPage_Loaded;
    }

    /// <summary>
    /// ページのViewModel。
    /// </summary>
    public NetworksViewModel ViewModel { get; }

    private async void NetworksPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private async void BtnDeleteNetwork_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: NetworkRowViewModel row })
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _resourceLoader.GetString("DeleteNetworkConfirmationDialog_Title"),
            Content = string.Format(_resourceLoader.GetString("DeleteNetworkConfirmationDialog_Message"), row.Name),
            PrimaryButtonText = _resourceLoader.GetString("DeleteNetworkConfirmationDialog_PrimaryButtonText"),
            CloseButtonText = _resourceLoader.GetString("DeleteNetworkConfirmationDialog_CloseButtonText"),
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
