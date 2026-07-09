// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WslContainersDesktop_App.ViewModels;

namespace WslContainersDesktop_App.Windows;

/// <summary>
/// シェルパネルの内容を大きな個別ウィンドウとして表示する。<see cref="ContainersViewModel"/>の
/// <see cref="ContainersViewModel.ShellOutput"/>等を小さいシェルパネルと共有するため、常に同じ状態が
/// 同期して表示される(状態を複製しない)。
/// </summary>
/// <remarks>
/// タイトルバーの閉じるボタン・ウィンドウ内のCloseボタンのどちらでこのウィンドウを閉じても、
/// シェルセッション自体は切断されない(このウィンドウを閉じる操作は「このウィンドウを閉じるだけ」
/// であり、シェルセッションの終了とは意図的に分離している)。シェルセッションを終了するには、
/// 小さいシェルパネル側の<see cref="ContainersViewModel.CloseShellCommand"/>に配線されたCloseボタンを使う。
/// </remarks>
public sealed partial class ShellWindow : Window
{
    private const int DefaultWidthInDips = 1000;
    private const int DefaultHeightInDips = 700;

    public ShellWindow(ContainersViewModel viewModel)
    {
        ViewModel = viewModel;

        InitializeComponent();

        Title = new global::Windows.ApplicationModel.Resources.ResourceLoader().GetString("ShellWindow.Title");

        // MainWindowと同じ、アプリアイコン付きのカスタムTitleBarをTall高さで表示する。
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(ShellWindowTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");

        // DPIスケールはXamlRootが確定してからでないと取得できない。Activatedイベントは
        // ウィンドウ再生成時(閉じて再度開いたとき)にXamlRoot確立前に発火することがあり、
        // Content.XamlRootへの直接アクセスがNullReferenceExceptionでクラッシュする不具合が
        // 実機で確認された。RootGridのLoadedイベントはXamlRoot確立後にのみ発火するため、
        // こちらを使う。
        RootGrid.Loaded += ShellWindow_RootGrid_Loaded;
    }

    public ContainersViewModel ViewModel { get; }

    private void ShellWindow_RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        RootGrid.Loaded -= ShellWindow_RootGrid_Loaded;

        var scale = RootGrid.XamlRoot.RasterizationScale;
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(
            (int)(DefaultWidthInDips * scale),
            (int)(DefaultHeightInDips * scale)));
    }

    /// <summary>
    /// Closeボタン押下時のハンドラ。シェルセッションは切断せず、このウィンドウを閉じるだけにする。
    /// </summary>
    private void BtnCloseShell_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// x:Bind用のbool→Visibility変換関数。値がtrueのとき表示する。
    /// </summary>
    public Visibility ToVisibleWhenTrue(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
}
