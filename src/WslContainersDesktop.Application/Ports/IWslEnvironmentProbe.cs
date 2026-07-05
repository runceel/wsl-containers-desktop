using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Ports;

/// <summary>
/// WSL環境の生の事実を観測するアウトバウンドポート。要件充足の判定は行わない。
/// </summary>
public interface IWslEnvironmentProbe
{
    /// <summary>
    /// 現在のWSL環境情報を取得する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<WslEnvironmentInfo> GetEnvironmentInfoAsync(CancellationToken cancellationToken = default);
}
