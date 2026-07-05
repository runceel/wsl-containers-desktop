namespace WslContainersDesktop.Application.Exceptions;

/// <summary>
/// WSL設定（<c>.wslconfig</c>）やWSL環境情報へのアクセス・解析に失敗したことを表す例外。
/// </summary>
public sealed class WslSettingsAccessException : SettingsException
{
    /// <summary>
    /// <see cref="WslSettingsAccessException"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="message">エラーメッセージ。</param>
    /// <param name="innerException">内部例外（存在する場合）。</param>
    public WslSettingsAccessException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
