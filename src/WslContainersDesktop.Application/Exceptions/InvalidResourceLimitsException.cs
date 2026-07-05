namespace WslContainersDesktop.Application.Exceptions;

/// <summary>
/// リソース制限として不正な値（正の整数でない値）が指定されたことを表す例外。
/// </summary>
public sealed class InvalidResourceLimitsException : SettingsException
{
    /// <summary>
    /// <see cref="InvalidResourceLimitsException"/> の新しいインスタンスを既定のメッセージで初期化する。
    /// </summary>
    public InvalidResourceLimitsException()
        : base("リソース制限には正の整数を指定するか、未指定（WSL既定）にしてください。")
    {
    }

    /// <summary>
    /// <see cref="InvalidResourceLimitsException"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="message">エラーメッセージ。</param>
    /// <param name="innerException">内部例外（存在する場合）。</param>
    public InvalidResourceLimitsException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
