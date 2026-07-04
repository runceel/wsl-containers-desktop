namespace WslContainersDesktop.Domain;

/// <summary>
/// WSL Containers上のコンテナ詳細情報を表すエンティティ。
/// </summary>
/// <param name="Id">コンテナID。</param>
/// <param name="Name">コンテナ名。</param>
/// <param name="Image">使用イメージ名またはイメージID。</param>
/// <param name="State">現在の状態。</param>
/// <param name="CreatedAt">作成日時。</param>
/// <param name="Command">実行コマンド。</param>
/// <param name="Entrypoint">エントリポイント。</param>
/// <param name="Ports">ポートマッピング。</param>
/// <param name="Environment">環境変数。</param>
/// <param name="Mounts">マウント構成。</param>
/// <param name="Networks">接続ネットワーク。</param>
/// <param name="RunState">直近の実行状態。</param>
public sealed record ContainerDetail(
    string Id,
    string Name,
    string Image,
    ContainerState State,
    DateTimeOffset CreatedAt,
    string? Command,
    string? Entrypoint,
    IReadOnlyList<ContainerPortMapping> Ports,
    IReadOnlyList<ContainerEnvironmentVariable> Environment,
    IReadOnlyList<ContainerMount> Mounts,
    IReadOnlyList<ContainerNetwork> Networks,
    ContainerRunState RunState);

/// <summary>
/// コンテナのポートマッピング。
/// </summary>
public sealed record ContainerPortMapping(string? HostAddress, ushort? HostPort, ushort ContainerPort, string Protocol);

/// <summary>
/// コンテナに設定された環境変数。
/// </summary>
public sealed record ContainerEnvironmentVariable(string Name, string Value);

/// <summary>
/// コンテナのマウント構成。
/// </summary>
public sealed record ContainerMount(string Type, string Source, string Target, bool IsReadOnly);

/// <summary>
/// コンテナが接続しているネットワーク。
/// </summary>
public sealed record ContainerNetwork(string Name, string? IpAddress);

/// <summary>
/// コンテナの直近の実行状態。
/// </summary>
public sealed record ContainerRunState(DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt, int? ExitCode, string? HealthStatus);
