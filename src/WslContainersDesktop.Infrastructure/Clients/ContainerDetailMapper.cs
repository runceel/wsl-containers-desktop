using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc container inspect</c> のJSON DTO（<see cref="ContainerInspectDto"/>）を
/// ドメインの <see cref="ContainerDetail"/> へマッピングする。
/// </summary>
internal static class ContainerDetailMapper
{
    public static ContainerDetail MapDetail(ContainerInspectDto item)
    {
        return new ContainerDetail(
            Id: item.Id,
            Name: item.Name,
            Image: item.Image,
            State: item.State.Running ? ContainerState.Running : ContainerState.Stopped,
            CreatedAt: CliDateTimeParsing.ParseDateTimeOffsetOrDefault(item.Created),
            Command: JoinCommand(item.Config.Cmd),
            Entrypoint: JoinCommand(item.Config.Entrypoint),
            Ports: MapPorts(item.Ports),
            Environment: MapEnvironment(item.Config.Env),
            Mounts: item.Mounts
                .Select(mount => new ContainerMount(mount.Type, mount.Source, mount.Destination, IsReadOnly: !mount.ReadWrite))
                .ToList(),
            Networks: item.NetworkSettings.Networks
                .Select(network => new ContainerNetwork(network.Key, EmptyToNull(network.Value.IPAddress)))
                .ToList(),
            RunState: new ContainerRunState(
                CliDateTimeParsing.ParseNullableDateTimeOffset(item.State.StartedAt),
                CliDateTimeParsing.ParseNullableDateTimeOffset(item.State.FinishedAt),
                item.State.ExitCode,
                HealthStatus: null));
    }

    private static IReadOnlyList<ContainerPortMapping> MapPorts(Dictionary<string, List<InspectPortBindingDto>> ports)
    {
        var mappings = new List<ContainerPortMapping>();
        foreach (var (portAndProtocol, bindings) in ports)
        {
            var (containerPort, protocol) = ParsePortAndProtocol(portAndProtocol);
            if (bindings.Count == 0)
            {
                mappings.Add(new ContainerPortMapping(null, null, containerPort, protocol));
                continue;
            }

            foreach (var binding in bindings)
            {
                mappings.Add(new ContainerPortMapping(
                    EmptyToNull(binding.HostIp),
                    ParseNullableUInt16(binding.HostPort),
                    containerPort,
                    protocol));
            }
        }

        return mappings;
    }

    private static IReadOnlyList<ContainerEnvironmentVariable> MapEnvironment(IReadOnlyList<string>? environment)
    {
        if (environment is null)
        {
            return [];
        }

        return environment
            .Select(value =>
            {
                var separatorIndex = value.IndexOf('=');
                return separatorIndex < 0
                    ? new ContainerEnvironmentVariable(value, string.Empty)
                    : new ContainerEnvironmentVariable(value[..separatorIndex], value[(separatorIndex + 1)..]);
            })
            .ToList();
    }

    private static (ushort ContainerPort, string Protocol) ParsePortAndProtocol(string value)
    {
        var parts = value.Split('/', 2);
        var containerPort = ushort.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
        var protocol = parts.Length == 2 ? parts[1] : string.Empty;
        return (containerPort, protocol);
    }

    private static ushort? ParseNullableUInt16(string value)
    {
        return ushort.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static string? JoinCommand(IReadOnlyList<string>? values)
    {
        return values is { Count: > 0 } ? string.Join(' ', values) : null;
    }

    private static string? EmptyToNull(string value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
