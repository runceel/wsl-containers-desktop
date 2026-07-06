using System.Text.Json.Serialization;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc stats --format json</c> の1要素に対応するJSON DTO。
/// 実機 wslc 2.9.3.0 の出力で確認済み（例: <c>{"ID":"...","Name":"...","CPUPerc":"0.00%","MemUsage":"1.82 MiB / 15.37 GiB", ...}</c>）。
/// BlockIO/MemPerc/NetIO/PIDs は本機能では未使用のため意図的に無視する。
/// Public Preview のためフォーマットは変わりうる（docs/reference/wsl-containers-platform.md 参照）。
/// </summary>
internal sealed class ContainerStatsDto
{
    [JsonPropertyName("ID")]
    public string ID { get; set; } = string.Empty;

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("CPUPerc")]
    public string CPUPerc { get; set; } = string.Empty;

    [JsonPropertyName("MemUsage")]
    public string MemUsage { get; set; } = string.Empty;
}
