namespace WslContainersDesktop.Domain;

/// <summary>
/// WSL Containers上のコンテナのリソース使用量を表す値オブジェクト。
/// </summary>
/// <param name="ContainerId">コンテナID。</param>
/// <param name="Name">コンテナ名。</param>
/// <param name="CpuPercentage">CPU使用率。</param>
/// <param name="MemoryUsageBytes">メモリ使用量（バイト）。</param>
/// <param name="MemoryLimitBytes">メモリ制限（バイト）。</param>
public sealed record ContainerResourceUsage(
    string ContainerId,
    string Name,
    double CpuPercentage,
    long MemoryUsageBytes,
    long MemoryLimitBytes)
{
    /// <summary>
    /// メモリ使用率（パーセント）。
    /// </summary>
    public double MemoryPercentage => MemoryLimitBytes > 0 ? (double)MemoryUsageBytes / MemoryLimitBytes * 100 : 0;
}
