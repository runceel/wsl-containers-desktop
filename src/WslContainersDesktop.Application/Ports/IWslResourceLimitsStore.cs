using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Ports;

/// <summary>
/// WSL Containersのリソース制限を永続化ストア（<c>.wslconfig</c>）に対して読み書きする
/// アウトバウンドポート。
/// </summary>
public interface IWslResourceLimitsStore
{
    /// <summary>
    /// 現在永続化されているリソース制限を取得する。未設定の項目は <see langword="null"/>。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<WslResourceLimits> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したリソース制限を永続化する。
    /// </summary>
    /// <param name="limits">保存するリソース制限。<see langword="null"/> の項目はストアから除去される。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task SaveAsync(WslResourceLimits limits, CancellationToken cancellationToken = default);
}
