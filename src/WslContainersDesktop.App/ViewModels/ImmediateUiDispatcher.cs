namespace WslContainersDesktop_App.ViewModels;

/// <summary>
/// 現在のスレッドで処理を即時実行する <see cref="IUiDispatcher"/>。
/// </summary>
public sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public void Invoke(Action action) => action();
}
