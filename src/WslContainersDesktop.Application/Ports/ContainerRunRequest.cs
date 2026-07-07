namespace WslContainersDesktop.Application.Ports;

/// <summary>
/// コンテナーイメージから新しいコンテナーを起動する要求。
/// </summary>
public sealed class ContainerRunRequest
{
    /// <summary>
    /// 起動元のイメージ参照。
    /// </summary>
    public string ImageReference { get; set; } = string.Empty;

    /// <summary>
    /// 作成するコンテナー名。未指定の場合はランタイム既定名を使う。
    /// </summary>
    public string ContainerName { get; set; } = string.Empty;

    /// <summary>
    /// 停止時にコンテナーを自動削除するかどうか。
    /// </summary>
    public bool RemoveWhenStopped { get; set; }

    /// <summary>
    /// 公開するポートマッピング。
    /// </summary>
    public IReadOnlyList<string> PortMappings { get; set; } = [];

    /// <summary>
    /// コンテナーに渡す環境変数。
    /// </summary>
    public IReadOnlyList<string> EnvironmentVariables { get; set; } = [];

    /// <summary>
    /// イメージ既定コマンドの代わりに実行するシェルコマンド。
    /// </summary>
    public string Command { get; set; } = string.Empty;
}
