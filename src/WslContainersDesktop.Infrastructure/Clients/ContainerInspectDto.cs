using System.Text.Json.Serialization;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc container inspect</c> の1要素に対応するJSON DTO。
/// </summary>
internal sealed class ContainerInspectDto
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Created")]
    public string Created { get; set; } = string.Empty;

    [JsonPropertyName("Image")]
    public string Image { get; set; } = string.Empty;

    [JsonPropertyName("State")]
    public ContainerInspectStateDto State { get; set; } = new();

    [JsonPropertyName("Config")]
    public ContainerConfigDto Config { get; set; } = new();

    [JsonPropertyName("Ports")]
    public Dictionary<string, List<InspectPortBindingDto>> Ports { get; set; } = [];

    [JsonPropertyName("Mounts")]
    public List<InspectMountDto> Mounts { get; set; } = [];

    [JsonPropertyName("NetworkSettings")]
    public InspectNetworkSettingsDto NetworkSettings { get; set; } = new();
}

internal sealed class ContainerInspectStateDto
{
    [JsonPropertyName("Status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("Running")]
    public bool Running { get; set; }

    [JsonPropertyName("ExitCode")]
    public int ExitCode { get; set; }

    [JsonPropertyName("StartedAt")]
    public string StartedAt { get; set; } = string.Empty;

    [JsonPropertyName("FinishedAt")]
    public string FinishedAt { get; set; } = string.Empty;
}

internal sealed class ContainerConfigDto
{
    [JsonPropertyName("Env")]
    public List<string>? Env { get; set; }

    [JsonPropertyName("Cmd")]
    public List<string>? Cmd { get; set; }

    [JsonPropertyName("Entrypoint")]
    public List<string>? Entrypoint { get; set; }
}

internal sealed class InspectPortBindingDto
{
    [JsonPropertyName("HostIp")]
    public string HostIp { get; set; } = string.Empty;

    [JsonPropertyName("HostPort")]
    public string HostPort { get; set; } = string.Empty;
}

internal sealed class InspectMountDto
{
    [JsonPropertyName("Type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("Source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("Destination")]
    public string Destination { get; set; } = string.Empty;

    [JsonPropertyName("ReadWrite")]
    public bool ReadWrite { get; set; }
}

internal sealed class InspectNetworkSettingsDto
{
    [JsonPropertyName("Networks")]
    public Dictionary<string, InspectEndpointSettingsDto> Networks { get; set; } = [];
}

internal sealed class InspectEndpointSettingsDto
{
    [JsonPropertyName("IPAddress")]
    public string IPAddress { get; set; } = string.Empty;
}
