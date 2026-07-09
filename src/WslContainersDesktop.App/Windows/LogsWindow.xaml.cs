// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WslContainersDesktop_App.ViewModels;

namespace WslContainersDesktop_App.Windows;

/// <summary>
/// ログパネルの内容を大きな個別ウィンドウとして表示する。<see cref="ContainersViewModel"/>の
/// <see cref="ContainersViewModel.LogLines"/>等を小さいログパネルと共有するため、常に同じ状態が
/// 同期して表示される(状態を複製しない)。
/// </summary>
/// <remarks>
/// タイトルバーの閉じるボタン・ウィンドウ内のCloseボタンのどちらでこのウィンドウを閉じても、
/// ログ追跡自体は停止しない(このウィンドウを閉じる操作は「このウィンドウを閉じるだけ」であり、
/// ログ追跡の停止とは意図的に分離している)。ログ追跡を停止するには、小さいログパネル側の
/// <see cref="ContainersViewModel.CloseLogsCommand"/>に配線されたCloseボタンを使う。
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

        // MainWindowと同じ、アプリアイコン付きのカスタムTitleBarをTall高さで表示する。
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(LogsWindowTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");

        // DPIスケールはXamlRootが確定してからでないと取得できない。Activatedイベントは
        // ウィンドウ再生成時(閉じて再度開いたとき)にXamlRoot確立前に発火することがあり、
        // Content.XamlRootへの直接アクセスがNullReferenceExceptionでクラッシュする不具合が
        // 実機で確認された。RootGridのLoadedイベントはXamlRoot確立後にのみ発火するため、
        // こちらを使う。
        RootGrid.Loaded += LogsWindow_RootGrid_Loaded;
    }

    public ContainersViewModel ViewModel { get; }

    private void LogsWindow_RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        RootGrid.Loaded -= LogsWindow_RootGrid_Loaded;

        var scale = RootGrid.XamlRoot.RasterizationScale;
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(
            (int)(DefaultWidthInDips * scale),
            (int)(DefaultHeightInDips * scale)));
    }

    /// <summary>
    /// Closeボタン押下時のハンドラ。ログ追跡は停止せず、このウィンドウを閉じるだけにする。
    /// </summary>
    private void BtnCloseLogs_Click(object sender, RoutedEventArgs e)
    {
        Close();
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
