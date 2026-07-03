using System.Text.Json.Serialization;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc list -a --format json</c> の1要素に対応するJSON DTO。
/// </summary>
internal sealed class ContainerListItemDto
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Image")]
    public string Image { get; set; } = string.Empty;

    /// <summary>
    /// wslc SDKの <c>ContainerState</c> 列挙体の数値
    /// （Invalid=0, Created=1, Running=2, Exited=3, Deleted=4）。
    /// </summary>
    [JsonPropertyName("State")]
    public int State { get; set; }

    [JsonPropertyName("CreatedAt")]
    public long CreatedAt { get; set; }
}
