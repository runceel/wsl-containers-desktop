namespace WslContainersDesktop.Domain;

/// <summary>
/// WSL Containersとの連携状態を表す値オブジェクト。要件充足の判定結果を含む。
/// </summary>
/// <param name="WslVersion">検出したWSLのバージョン文字列。検出できない場合は <see langword="null"/>。</param>
/// <param name="IsWslContainersAvailable">WSL Containers（<c>wslc</c>）が利用可能かどうか。</param>
/// <param name="MeetsRequirements">WSL Containersを利用するための要件を満たしているかどうか。</param>
public sealed record WslIntegrationStatus(
    string? WslVersion,
    bool IsWslContainersAvailable,
    bool MeetsRequirements)
{
    /// <summary>
    /// WSLが検出されているかどうか。
    /// </summary>
    public bool IsWslDetected => !string.IsNullOrEmpty(WslVersion);

    /// <summary>
    /// リソース制限を構成できる状態かどうか。
    /// </summary>
    public bool CanConfigureResources => MeetsRequirements;
}
