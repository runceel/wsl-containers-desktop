namespace WslContainersDesktop.Domain;

/// <summary>
/// WSL環境から観測できる生の事実を表す値オブジェクト。要件充足の判定は行わない。
/// </summary>
/// <param name="WslVersion">検出したWSLのバージョン文字列。検出できない場合は <see langword="null"/>。</param>
/// <param name="IsWslContainersAvailable">WSL Containers（<c>wslc</c>）が利用可能かどうか。</param>
public sealed record WslEnvironmentInfo(string? WslVersion, bool IsWslContainersAvailable)
{
    /// <summary>
    /// WSLが検出されているかどうか。
    /// </summary>
    public bool IsWslDetected => !string.IsNullOrEmpty(WslVersion);
}
