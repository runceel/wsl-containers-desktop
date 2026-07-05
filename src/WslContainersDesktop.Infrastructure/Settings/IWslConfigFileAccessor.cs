namespace WslContainersDesktop.Infrastructure.Settings;

/// <summary>
/// <c>%USERPROFILE%\.wslconfig</c> の内容を読み書きする低レベルseam。
/// テストで実ファイルに依存せず <see cref="WslConfigResourceLimitsStore"/> のロジックを検証できるようにする。
/// </summary>
public interface IWslConfigFileAccessor
{
    /// <summary>
    /// 設定ファイルの内容を読み取る。ファイルが存在しない場合は <see langword="null"/> を返す。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<string?> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 設定ファイルへ内容を書き込む。書き込みは原子的に行われる。
    /// </summary>
    /// <param name="content">書き込む内容。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task WriteAsync(string content, CancellationToken cancellationToken = default);
}
