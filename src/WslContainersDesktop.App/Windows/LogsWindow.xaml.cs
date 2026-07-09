// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using WslContainersDesktop_App.ViewModels;

namespace WslContainersDesktop_App.Windows;

/// <summary>
/// ログパネルの内容を大きな個別ウィンドウとして表示する。<see cref="ContainersViewModel"/>の
/// <see cref="ContainersViewModel.LogLines"/>等を小さいログパネルと共有するため、常に同じ状態が
/// 同期して表示される(状態を複製しない)。
/// </summary>
/// <remarks>
/// タイトルバーの閉じるボタンでこのウィンドウを閉じても、ログ追跡自体は停止しない
/// (ログ追跡の停止は本ウィンドウ・小さいログパネルどちらからでも<see cref="ContainersViewModel.CloseLogsCommand"/>
/// で行う。ウィンドウを閉じる操作とセッションを閉じる操作は意図的に分離している)。
/// </remarks>
public sealed partial class LogsWindow : Window
{
    private const int DefaultWidthInDips = 1000;
    private const int DefaultHeightInDips = 700;

    public LogsWindow(ContainersViewModel viewModel)
    {
        ViewModel = viewModel;

        InitializeComponent();

        Title = new global::Windows.ApplicationModel.Resources.ResourceLoader().GetString("LogsWindow.Title");

        // DPIスケールはXamlRootが確定するActivated後でないと取得できないため、初回のみここでリサイズする。
        Activated += LogsWindow_Activated;
    }

    public ContainersViewModel ViewModel { get; }

    private void LogsWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= LogsWindow_Activated;

        var scale = Content.XamlRoot.RasterizationScale;
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(
            (int)(DefaultWidthInDips * scale),
            (int)(DefaultHeightInDips * scale)));
    }

    /// <summary>
    /// x:Bind用のbool→Visibility変換関数。値がtrueのとき表示する。
    /// </summary>
    public Visibility ToVisibleWhenTrue(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// x:Bind用のbool→Visibility変換関数。値がfalseのとき表示する。
    /// </summary>
    public Visibility ToVisibleWhenFalse(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
}
