using System.Text.Json.Serialization;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc image list --format json --no-trunc</c> の1要素に対応するJSON DTO。
/// </summary>
internal sealed class ImageListItemDto
{
    [JsonPropertyName("Created")]
    public long Created { get; set; }

    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("Repository")]
    public string Repository { get; set; } = string.Empty;

    [JsonPropertyName("Size")]
    public long Size { get; set; }

    [JsonPropertyName("Tag")]
    public string Tag { get; set; } = string.Empty;
}
