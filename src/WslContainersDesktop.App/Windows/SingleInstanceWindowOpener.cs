// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace WslContainersDesktop_App.Windows;

/// <summary>
/// 「同種のウィンドウは常に1つだけ」を保証しつつ開くためのヘルパー。
/// </summary>
/// <remarks>
/// <typeparamref name="TWindow"/>を実際のWinUI <c>Window</c>型に固定せず、生成・Activate・
/// Closed購読をすべてデリゲート経由で受け取ることで、実ウィンドウを生成せずにMSTestで
/// 「初回生成」「既存ウィンドウの再利用」「Closed後の再生成」を検証できるようにしている。
/// </remarks>
/// <typeparam name="TWindow">開閉対象のウィンドウの型。</typeparam>
public sealed class SingleInstanceWindowOpener<TWindow>
    where TWindow : class
{
    private readonly Func<TWindow> _createWindow;
    private readonly Action<TWindow> _activateWindow;
    private readonly Action<TWindow, Action> _registerClosedHandler;

    private TWindow? _window;

    public SingleInstanceWindowOpener(
        Func<TWindow> createWindow,
        Action<TWindow> activateWindow,
        Action<TWindow, Action> registerClosedHandler)
    {
        _createWindow = createWindow;
        _activateWindow = activateWindow;
        _registerClosedHandler = registerClosedHandler;
    }

    /// <summary>
    /// ウィンドウが開いていなければ生成してActivateし、既に開いていれば既存のウィンドウを
    /// Activateするだけにとどめる(新規に複数開かない)。
    /// </summary>
    public void ShowOrActivate()
    {
        if (_window is null)
        {
            var window = _createWindow();
            _window = window;
            _registerClosedHandler(window, () => _window = null);
        }

        _activateWindow(_window);
    }
}
