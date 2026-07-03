namespace WslContainersDesktop_App.ViewModels;

/// <summary>
/// ViewModelからUIスレッド上で状態更新を行うための抽象。
/// </summary>
public interface IUiDispatcher
{
    /// <summary>
    /// 指定された処理をUIスレッドで実行する。
    /// </summary>
    /// <param name="action">実行する処理。</param>
    void Invoke(Action action);
}
