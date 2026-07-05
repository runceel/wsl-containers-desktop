namespace WslContainersDesktop.Application.Exceptions;

/// <summary>
/// 設定ユースケースで発生する例外の基底クラス。
/// </summary>
public abstract class SettingsException : Exception
{
    /// <summary>
    /// <see cref="SettingsException"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="message">エラーメッセージ。</param>
    /// <param name="innerException">内部例外（存在する場合）。</param>
    protected SettingsException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
