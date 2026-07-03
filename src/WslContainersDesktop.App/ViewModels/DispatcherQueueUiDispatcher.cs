using Microsoft.UI.Dispatching;

namespace WslContainersDesktop_App.ViewModels;

/// <summary>
/// WinUIの <see cref="DispatcherQueue"/> を使ってUIスレッドへ処理を送る。
/// </summary>
public sealed class DispatcherQueueUiDispatcher(DispatcherQueue dispatcherQueue) : IUiDispatcher
{
    public void Invoke(Action action)
    {
        if (!dispatcherQueue.TryEnqueue(() => action()))
        {
            throw new InvalidOperationException("UIスレッドへの処理の送信に失敗しました。");
        }
    }
}
