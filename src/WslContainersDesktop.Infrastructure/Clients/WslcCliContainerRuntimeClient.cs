using System.Globalization;
using System.Text.Json;
using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;
using WslContainersDesktop.Infrastructure.Cli;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc</c> CLIをプロセスとして呼び出すことで <see cref="IContainerRuntimeClient"/> を実装する。
/// ネイティブSDK（Microsoft.WSL.Containers）はコンテナ一覧の取得APIを提供していないため、
/// CLIのstdoutをJSONとしてパースする方式を採用している（ADR-0009参照）。
/// </summary>
public sealed class WslcCliContainerRuntimeClient(IWslcCliRunner cliRunner) : IContainerRuntimeClient
{
    /// <summary>
    /// wslc SDKの <c>ContainerState.Running</c> に対応する数値。
    /// </summary>
    private const int RunningStateValue = 2;

    /// <summary>
    /// メモリ使用量文字列（例: <c>"128MiB"</c>）を解析する際に用いる単位とバイト換算係数の対応表。
    /// 長い単位（TiB, GiB…）から順に評価することで、部分一致による誤判定を防ぐ。
    /// </summary>
    private static readonly (string Unit, double Multiplier)[] MemoryUnits =
    [
        ("TiB", 1024d * 1024 * 1024 * 1024),
        ("GiB", 1024d * 1024 * 1024),
        ("MiB", 1024d * 1024),
        ("KiB", 1024d),
        ("TB", 1000d * 1000 * 1000 * 1000),
        ("GB", 1000d * 1000 * 1000),
        ("MB", 1000d * 1000),
        ("kB", 1000d),
        ("B", 1d),
    ];

    public async Task<IReadOnlyList<Container>> ListContainersAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["list", "-a", "--format", "json"], cancellationToken);

        var items = DeserializeJsonList<ContainerListItemDto>(
            result,
            command: "list -a --format json",
            failureMessage: "コンテナ一覧の解析に失敗しました。");
        if (items is null)
        {
            return [];
        }

        return items
            .Select(item => new Container(
                Id: item.Id,
                Name: item.Name,
                Image: item.Image,
                State: item.State == RunningStateValue ? ContainerState.Running : ContainerState.Stopped,
                CreatedAt: DateTimeOffset.FromUnixTimeSeconds(item.CreatedAt)))
            .ToList();
    }

    public async Task<IReadOnlyList<ContainerImage>> ListImagesAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["image", "list", "--format", "json", "--no-trunc"], cancellationToken);

        var items = DeserializeJsonList<ImageListItemDto>(
            result,
            command: "image list --format json --no-trunc",
            failureMessage: "コンテナーイメージ一覧の解析に失敗しました。");
        if (items is null)
        {
            return [];
        }

        return items
            .Select(item => new ContainerImage(
                Id: item.Id,
                Repository: item.Repository,
                Tag: item.Tag,
                SizeBytes: item.Size,
                CreatedAt: DateTimeOffset.FromUnixTimeSeconds(item.Created)))
            .ToList();
    }

    public Task PullImageAsync(string imageReference, CancellationToken cancellationToken = default)
    {
        return RunAsync(["pull", imageReference], cancellationToken);
    }

    public Task DeleteImageAsync(string imageId, CancellationToken cancellationToken = default)
    {
        return RunAsync(["image", "remove", imageId], cancellationToken);
    }

    public async Task<IReadOnlyList<ContainerVolume>> ListVolumesAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["volume", "list", "--format", "json"], cancellationToken);

        var items = DeserializeJsonList<VolumeListItemDto>(
            result,
            command: "volume list --format json",
            failureMessage: "コンテナーボリューム一覧の解析に失敗しました。");
        if (items is null)
        {
            return [];
        }

        var volumes = new List<ContainerVolume>();
        foreach (var item in items)
        {
            volumes.Add(await InspectVolumeAsync(item, cancellationToken));
        }

        return volumes;
    }

    public Task CreateVolumeAsync(string name, CancellationToken cancellationToken = default)
    {
        return RunAsync(["volume", "create", name], cancellationToken);
    }

    public Task DeleteVolumeAsync(string name, CancellationToken cancellationToken = default)
    {
        return RunAsync(["volume", "remove", name], cancellationToken);
    }

    public async Task<IReadOnlyList<ContainerNetworkResource>> ListNetworksAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["network", "list", "--format", "json"], cancellationToken);

        var items = DeserializeJsonList<NetworkListItemDto>(
            result,
            command: "network list --format json",
            failureMessage: "コンテナーネットワーク一覧の解析に失敗しました。");
        if (items is null)
        {
            return [];
        }

        var networks = new List<ContainerNetworkResource>();
        foreach (var item in items)
        {
            networks.Add(await InspectNetworkAsync(item, cancellationToken));
        }

        return networks;
    }

    public Task CreateNetworkAsync(string name, CancellationToken cancellationToken = default)
    {
        return RunAsync(["network", "create", name], cancellationToken);
    }

    public Task DeleteNetworkAsync(string name, CancellationToken cancellationToken = default)
    {
        return RunAsync(["network", "remove", name], cancellationToken);
    }

    public Task StartAsync(string containerId, CancellationToken cancellationToken = default)
    {
        return RunAsync(["container", "start", containerId], cancellationToken);
    }

    public Task StopAsync(string containerId, CancellationToken cancellationToken = default)
    {
        return RunAsync(["container", "stop", containerId], cancellationToken);
    }

    public Task DeleteAsync(string containerId, CancellationToken cancellationToken = default)
    {
        // -f（強制削除）を付けない。これにより実行中のコンテナに対する削除はwslc自体が
        // 拒否するため、Application層での事前検証と合わせて二重に安全性が確保される
        // （ADR-0009参照）。
        return RunAsync(["container", "remove", containerId], cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetContainerLogsAsync(string containerId, CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["container", "logs", containerId], cancellationToken);
        var lines = SplitLogResult(result);
        return lines;
    }

    public async Task<ContainerDetail> GetContainerDetailAsync(string containerId, CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["container", "inspect", containerId], cancellationToken);

        var items = DeserializeJsonList<ContainerInspectDto>(
            result,
            command: $"container inspect {containerId}",
            failureMessage: "コンテナ詳細情報の解析に失敗しました。");
        var item = items?.FirstOrDefault();
        if (item is null)
        {
            throw new ContainerRuntimeException($"container inspect {containerId}", result.ExitCode, "コンテナ詳細情報が見つかりませんでした。");
        }

        return MapInspectDetail(item);
    }

    public async Task<IReadOnlyList<ContainerResourceUsage>> GetContainerStatsAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["stats", "--format", "json"], cancellationToken);

        var dtos = ParseStatsOutput(result);
        return dtos
            .Select(dto =>
            {
                var (usage, limit) = ParseMemoryUsage(dto.MemUsage);
                return new ContainerResourceUsage(
                    ContainerId: dto.ID,
                    Name: dto.Name,
                    CpuPercentage: ParseCpuPercentage(dto.CPUPerc),
                    MemoryUsageBytes: usage,
                    MemoryLimitBytes: limit);
            })
            .ToList();
    }

    public Task<IContainerExecSession> OpenExecSessionAsync(string containerId, CancellationToken cancellationToken = default)
    {
        return cliRunner.OpenInteractiveAsync(["container", "exec", "-i", containerId, "/bin/sh"], cancellationToken);
    }

    public async IAsyncEnumerable<string> FollowContainerLogsAsync(
        string containerId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var since = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        IReadOnlyList<string> arguments = ["container", "logs", "--since", since, "--follow", containerId];
        await using var enumerator = cliRunner.StreamLinesAsync(arguments, cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = await enumerator.MoveNextAsync();
            }
            catch (CliStreamException ex)
            {
                throw new ContainerRuntimeException(ex.Command, ex.ExitCode, ex.Message, ex);
            }

            if (!hasNext)
            {
                yield break;
            }

            yield return enumerator.Current.TrimEnd('\r');
        }
    }

    private async Task<CliResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var result = await cliRunner.RunAsync(arguments, cancellationToken);
        if (result.ExitCode != 0)
        {
            var command = string.Join(' ', arguments);
            var message = string.IsNullOrWhiteSpace(result.StandardError)
                ? $"コマンド '{command}' がエラーコード {result.ExitCode} で終了しました。"
                : result.StandardError.Trim();
            throw new ContainerRuntimeException(command, result.ExitCode, message);
        }

        return result;
    }

    private static List<TDto>? DeserializeJsonList<TDto>(CliResult result, string command, string failureMessage)
    {
        try
        {
            return JsonSerializer.Deserialize<List<TDto>>(result.StandardOutput);
        }
        catch (JsonException ex)
        {
            throw new ContainerRuntimeException(
                command: command,
                exitCode: result.ExitCode,
                message: failureMessage,
                innerException: ex);
        }
    }

    // 実機 wslc 2.9.3.0 `stats --format json` は JSON 配列を返す（Phase 5 確認）。
    // 将来のフォーマット差異に備え NDJSON 形式にもフォールバックする。
    private static List<ContainerStatsDto> ParseStatsOutput(CliResult result)
    {
        var stdout = result.StandardOutput.Trim();
        if (stdout.Length == 0)
        {
            return [];
        }

        try
        {
            if (stdout.StartsWith('['))
            {
                return JsonSerializer.Deserialize<List<ContainerStatsDto>>(stdout) ?? [];
            }

            var list = new List<ContainerStatsDto>();
            foreach (var line in stdout.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                var dto = JsonSerializer.Deserialize<ContainerStatsDto>(trimmed);
                if (dto is not null)
                {
                    list.Add(dto);
                }
            }

            return list;
        }
        catch (JsonException ex)
        {
            throw new ContainerRuntimeException(
                command: "stats --format json",
                exitCode: result.ExitCode,
                message: "コンテナのリソース使用量の解析に失敗しました。",
                innerException: ex);
        }
    }

    private static double ParseCpuPercentage(string raw)
    {
        var text = raw.Trim();
        if (text.EndsWith('%'))
        {
            text = text[..^1];
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static (long Usage, long Limit) ParseMemoryUsage(string raw)
    {
        var parts = raw.Split('/');
        var usage = ParseMemoryBytes(parts[0]);
        var limit = parts.Length > 1 ? ParseMemoryBytes(parts[1]) : 0;
        return (usage, limit);
    }

    private static long ParseMemoryBytes(string raw)
    {
        var text = raw.Trim();
        if (text.Length == 0)
        {
            return 0;
        }

        foreach (var (unit, multiplier) in MemoryUnits)
        {
            if (text.EndsWith(unit, StringComparison.Ordinal))
            {
                var numberText = text[..^unit.Length].Trim();
                return double.TryParse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                    ? (long)Math.Round(value * multiplier, MidpointRounding.AwayFromZero)
                    : 0;
            }
        }

        return 0;
    }

    private async Task<ContainerVolume> InspectVolumeAsync(VolumeListItemDto listItem, CancellationToken cancellationToken)
    {
        var result = await RunAsync(["volume", "inspect", listItem.Name], cancellationToken);

        var items = DeserializeJsonList<VolumeInspectDto>(
            result,
            command: $"volume inspect {listItem.Name}",
            failureMessage: "コンテナーボリューム詳細情報の解析に失敗しました。");
        var item = items?.FirstOrDefault();
        if (item is null)
        {
            return new ContainerVolume(listItem.Name, listItem.Driver, DateTimeOffset.MinValue, []);
        }

        return new ContainerVolume(
            Name: string.IsNullOrEmpty(item.Name) ? listItem.Name : item.Name,
            Driver: string.IsNullOrEmpty(item.Driver) ? listItem.Driver : item.Driver,
            CreatedAt: ParseDateTimeOffsetOrDefault(item.CreatedAt),
            ReferencingContainerNames: []);
    }

    private async Task<ContainerNetworkResource> InspectNetworkAsync(NetworkListItemDto listItem, CancellationToken cancellationToken)
    {
        var result = await RunAsync(["network", "inspect", listItem.Name], cancellationToken);

        var items = DeserializeJsonList<NetworkInspectDto>(
            result,
            command: $"network inspect {listItem.Name}",
            failureMessage: "コンテナーネットワーク詳細情報の解析に失敗しました。");
        var item = items?.FirstOrDefault();
        if (item is null)
        {
            return new ContainerNetworkResource(listItem.Name, listItem.Driver, DateTimeOffset.MinValue, [], listItem.IsSystem);
        }

        return new ContainerNetworkResource(
            Name: string.IsNullOrEmpty(item.Name) ? listItem.Name : item.Name,
            Driver: string.IsNullOrEmpty(item.Driver) ? listItem.Driver : item.Driver,
            CreatedAt: ParseDateTimeOffsetOrDefault(item.CreatedAt),
            ConnectedContainerNames: [],
            IsSystem: listItem.IsSystem || item.IsSystem);
    }

    private static IReadOnlyList<string> SplitLogLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    private static IReadOnlyList<string> SplitLogResult(CliResult result)
    {
        var lines = new List<string>();
        lines.AddRange(SplitLogLines(result.StandardOutput));
        lines.AddRange(SplitLogLines(result.StandardError));
        return lines;
    }

    private static ContainerDetail MapInspectDetail(ContainerInspectDto item)
    {
        return new ContainerDetail(
            Id: item.Id,
            Name: item.Name,
            Image: item.Image,
            State: item.State.Running ? ContainerState.Running : ContainerState.Stopped,
            CreatedAt: ParseDateTimeOffsetOrDefault(item.Created),
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
                ParseNullableDateTimeOffset(item.State.StartedAt),
                ParseNullableDateTimeOffset(item.State.FinishedAt),
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

    private static DateTimeOffset ParseDateTimeOffsetOrDefault(string value)
    {
        return ParseNullableDateTimeOffset(value) ?? DateTimeOffset.MinValue;
    }

    private static DateTimeOffset? ParseNullableDateTimeOffset(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("0001-01-01", StringComparison.Ordinal))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal,
            out var result)
            ? result.ToUniversalTime()
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
