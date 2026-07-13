// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using WslContainersDesktop_App.ViewModels;

namespace WslContainersDesktop_App.Windows;

/// <summary>
/// LogsWindow/ShellWindowの単一インスタンス生成・再利用を担うDIのSingletonサービス。
/// </summary>
/// <remarks>
/// ContainersPageはFrame.Navigateのたびに作り直されるため、ウィンドウ参照をページの
/// フィールドとして保持すると、ページを離れて戻ってきた際に追跡が切れてしまう。
/// このクラスをDIのSingletonとして登録し、アプリ全体で寿命を共有することで、
/// どのタイミングでContainersPageに遷移してもポップアウトウィンドウの状態を維持できる。
/// 生成/再利用/Closed後の再生成ロジック本体は<see cref="SingleInstanceWindowOpener{TWindow}"/>に
/// 委譲しており、そちらは独立してMSTestで検証済みのため、本クラス自体は薄い合成のみを行う。
/// </remarks>
public sealed class ContainerAuxiliaryWindowManager
{
    private readonly SingleInstanceWindowOpener<LogsWindow> _logsWindowOpener;
    private readonly SingleInstanceWindowOpener<ShellWindow> _shellWindowOpener;
    private readonly ContainersViewModel _viewModel;

    public ContainerAuxiliaryWindowManager(ContainersViewModel viewModel)
    {
        _viewModel = viewModel;
        _logsWindowOpener = new SingleInstanceWindowOpener<LogsWindow>(
            createWindow: () => new LogsWindow(viewModel),
            activateWindow: window => window.Activate(),
            registerClosedHandler: (window, onClosed) => window.Closed += (_, _) => onClosed());

        _shellWindowOpener = new SingleInstanceWindowOpener<ShellWindow>(
            createWindow: () => new ShellWindow(viewModel),
            activateWindow: window => window.Activate(),
            registerClosedHandler: (window, onClosed) => window.Closed += (_, _) => onClosed());
    }

    /// <summary>
    /// ログのポップアウトウィンドウを開き、対応するインラインのログパネルを非表示にする。
    /// 既に開いていれば新規に開かず、既存ウィンドウをActivateしてからパネルを非表示にする。
    /// </summary>
    public void ShowLogsWindow()
    {
        _logsWindowOpener.ShowOrActivate();
        _viewModel.HideLogPanel();
    }

    /// <summary>
    /// シェルのポップアウトウィンドウを開き、対応するインラインのシェルパネルを非表示にする。
    /// 既に開いていれば新規に開かず、既存ウィンドウをActivateしてからパネルを非表示にする。
    /// </summary>
    public void ShowShellWindow()
    {
        _shellWindowOpener.ShowOrActivate();
        _viewModel.HideShellPanel();
    }
}
