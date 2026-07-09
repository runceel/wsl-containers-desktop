// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WslContainersDesktop_App.ViewModels;
using WslContainersDesktop_App.Windows;
using Windows.ApplicationModel.Resources;

namespace WslContainersDesktop_App.Pages;

public sealed partial class ContainersPage : Page
{
    private readonly ResourceLoader _resourceLoader = new();
    private readonly ContainerAuxiliaryWindowManager _windowManager;

    public ContainersPage()
    {
        // Frame.Navigate(Type)によるページ遷移はパラメーターレスコンストラクタを要求するため、
        // ADR-0010に基づきCompositionRoot(App)のServiceProvider経由でViewModelを解決する。
        ViewModel = ((App)Application.Current).Services.GetRequiredService<ContainersViewModel>();

        // ページはNavigateのたびに作り直されるため、ポップアウトウィンドウの参照はページの
        // フィールドではなくDIのSingletonである_windowManagerが保持し続ける。
        _windowManager = ((App)Application.Current).Services.GetRequiredService<ContainerAuxiliaryWindowManager>();

        InitializeComponent();

        Loaded += ContainersPage_Loaded;
    }

    public ContainersViewModel ViewModel { get; }

    private async void ContainersPage_Loaded(object sender, RoutedEventArgs e)
    {
        // ページ表示時に最新のコンテナ一覧を取得する（受け入れ基準:
        // 「アプリ起動後、コンテナ一覧画面に遷移すると...コンテナがすべて表示される」）。
        await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (GetContainerRow(sender) is not { } row)
        {
            return;
        }

        // 削除は取り消せない操作のため、実行前に確認ダイアログを表示する
        // （受け入れ基準: 「誤操作を防ぐ確認の上でコンテナが削除され」）。
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _resourceLoader.GetString("DeleteConfirmationDialog_Title"),
            Content = string.Format(_resourceLoader.GetString("DeleteConfirmationDialog_Message"), row.Name),
            PrimaryButtonText = _resourceLoader.GetString("DeleteConfirmationDialog_PrimaryButtonText"),
            CloseButtonText = _resourceLoader.GetString("DeleteConfirmationDialog_CloseButtonText"),
            DefaultButton = ContentDialogButton.Close,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.DeleteCommand.ExecuteAsync(row);
        }
    }

    private async void BtnLogs_Click(object sender, RoutedEventArgs e)
    {
        if (GetContainerId(sender) is { } containerId)
        {
            await ViewModel.OpenLogsCommand.ExecuteAsync(containerId);
        }
    }

    private async void BtnDetails_Click(object sender, RoutedEventArgs e)
    {
        if (GetContainerId(sender) is { } containerId)
        {
            await ViewModel.OpenDetailsCommand.ExecuteAsync(containerId);
        }
    }

    private async void BtnShell_Click(object sender, RoutedEventArgs e)
    {
        if (GetContainerId(sender) is { } containerId)
        {
            await ViewModel.OpenShellCommand.ExecuteAsync(containerId);
        }
    }

    private async void MenuStart_Click(object sender, RoutedEventArgs e)
    {
        if (GetContainerRow(sender) is { } row)
        {
            await ViewModel.StartCommand.ExecuteAsync(row);
        }
    }

    private async void MenuStop_Click(object sender, RoutedEventArgs e)
    {
        if (GetContainerRow(sender) is { } row)
        {
            await ViewModel.StopCommand.ExecuteAsync(row);
        }
    }

    private async void MenuRestart_Click(object sender, RoutedEventArgs e)
    {
        if (GetContainerRow(sender) is { } row)
        {
            await ViewModel.RestartCommand.ExecuteAsync(row);
        }
    }

    private void BtnOpenLogsWindow_Click(object sender, RoutedEventArgs e)
    {
        _windowManager.ShowLogsWindow();
    }

    private void BtnOpenShellWindow_Click(object sender, RoutedEventArgs e)
    {
        _windowManager.ShowShellWindow();
    }

    private static ContainerRowViewModel? GetContainerRow(object sender)
    {
        return sender switch
        {
            MenuFlyoutItem { CommandParameter: ContainerRowViewModel row } => row,
            Button { CommandParameter: ContainerRowViewModel row } => row,
            FrameworkElement { DataContext: ContainerRowViewModel row } => row,
            _ => null,
        };
    }

    private static string? GetContainerId(object sender)
    {
        return sender switch
        {
            Button { CommandParameter: string containerId } => containerId,
            MenuFlyoutItem { CommandParameter: string containerId } => containerId,
            _ => null,
        };
    }

    /// <summary>
    /// x:Bind用のbool→Visibility変換関数。値がtrueのとき表示する。
    /// </summary>
    public Visibility ToVisibleWhenTrue(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// x:Bind用のbool→Visibility変換関数。値がfalseのとき表示する。
    /// </summary>
    public Visibility ToVisibleWhenFalse(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>
    /// x:Bind用のnull/空判定関数。<see cref="InfoBar.IsOpen"/>の表示制御に使用する。
    /// </summary>
    public bool IsNotNullOrEmpty(string? value) => !string.IsNullOrEmpty(value);

}
