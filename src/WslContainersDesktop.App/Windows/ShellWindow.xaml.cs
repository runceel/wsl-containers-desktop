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

        // DPIスケールはXamlRootが確定するActivated後でないと取得できないため、初回のみここでリサイズする。
        Activated += ShellWindow_Activated;
    }

    public ContainersViewModel ViewModel { get; }

    private void ShellWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= ShellWindow_Activated;

        var scale = Content.XamlRoot.RasterizationScale;
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
