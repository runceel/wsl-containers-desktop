using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Ports;

/// <summary>
/// ダッシュボードの各セクションを保持するスナップショット。
/// </summary>
public sealed class DashboardSnapshot
{
    /// <summary>
    /// コンテナ一覧セクション。
    /// </summary>
    public DashboardSection<IReadOnlyList<Container>> Containers { get; set; } = new();

    /// <summary>
    /// イメージ一覧セクション。
    /// </summary>
    public DashboardSection<IReadOnlyList<ContainerImage>> Images { get; set; } = new();

    /// <summary>
    /// ボリューム一覧セクション。
    /// </summary>
    public DashboardSection<IReadOnlyList<ContainerVolume>> Volumes { get; set; } = new();

    /// <summary>
    /// ネットワーク一覧セクション。
    /// </summary>
    public DashboardSection<IReadOnlyList<ContainerNetworkResource>> Networks { get; set; } = new();

    /// <summary>
    /// リソース使用量一覧セクション。
    /// </summary>
    public DashboardSection<IReadOnlyList<ContainerResourceUsage>> Stats { get; set; } = new();
}

/// <summary>
/// ダッシュボードで個別に取得・表示するセクション値。
/// </summary>
/// <typeparam name="T">セクションの値型。</typeparam>
public sealed class DashboardSection<T>
{
    /// <summary>
    /// セクション値。
    /// </summary>
    public T Value { get; set; } = default!;

    /// <summary>
    /// セクション取得時に発生した例外。
    /// </summary>
    public Exception? Exception { get; set; }
}
