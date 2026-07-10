using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Ports;

/// <summary>
/// ダッシュボードのスナップショットを取得するためのポート。
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// ダッシュボードで表示する情報のスナップショットを取得する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
