using System.Text.Json.Serialization;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc network list --format json</c> の1要素に対応するJSON DTO。
/// </summary>
internal sealed class NetworkListItemDto
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Driver")]
    public string Driver { get; set; } = string.Empty;

    [JsonPropertyName("IsSystem")]
    public bool IsSystem { get; set; }
}
