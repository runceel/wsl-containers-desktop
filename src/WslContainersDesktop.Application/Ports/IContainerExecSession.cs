namespace WslContainersDesktop.Application.Ports;

/// <summary>
/// 稼働中コンテナ内で開かれた対話的なexecセッション。
/// </summary>
public interface IContainerExecSession
{
    /// <summary>
    /// セッションが閉じているかどうか。
    /// </summary>
    bool IsClosed { get; }

    /// <summary>
    /// セッションの標準出力・標準エラーをチャンク単位で読み取る。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    IAsyncEnumerable<string> ReadOutputAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// シェルへコマンドを送信する。
    /// </summary>
    /// <param name="command">送信するコマンド。改行は実装側で付与する。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task SendCommandAsync(string command, CancellationToken cancellationToken = default);

    /// <summary>
    /// セッションを閉じる。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task CloseAsync(CancellationToken cancellationToken = default);
}
