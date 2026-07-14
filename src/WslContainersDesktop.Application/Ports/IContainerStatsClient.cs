using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Ports;

/// <summary>
/// コンテナ統計情報取得操作を抽象化するアウトバウンドポート。
/// Infrastructure層で実装される。
/// </summary>
public interface IContainerStatsClient
{
    /// <summary>
    /// 現在稼働中のコンテナのリソース使用量を取得する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<IReadOnlyList<ContainerResourceUsage>> GetContainerStatsAsync(CancellationToken cancellationToken = default);
}
