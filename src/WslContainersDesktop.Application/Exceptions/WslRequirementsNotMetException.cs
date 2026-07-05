namespace WslContainersDesktop.Application.Exceptions;

/// <summary>
/// WSL Containersの要件を満たしていない環境でリソース制限の変更が要求されたことを表す例外。
/// </summary>
public sealed class WslRequirementsNotMetException : SettingsException
{
    /// <summary>
    /// <see cref="WslRequirementsNotMetException"/> の新しいインスタンスを既定のメッセージで初期化する。
    /// </summary>
    public WslRequirementsNotMetException()
        : base("WSL Containersの要件を満たしていないため、リソース制限を変更できません。")
    {
    }

    /// <summary>
    /// <see cref="WslRequirementsNotMetException"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="message">エラーメッセージ。</param>
    /// <param name="innerException">内部例外（存在する場合）。</param>
    public WslRequirementsNotMetException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
