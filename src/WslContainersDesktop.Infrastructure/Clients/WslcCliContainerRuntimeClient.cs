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

    public async Task<IReadOnlyList<Container>> ListContainersAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["list", "-a", "--format", "json"], cancellationToken);

        List<ContainerListItemDto>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<ContainerListItemDto>>(result.StandardOutput);
        }
        catch (JsonException ex)
        {
            throw new ContainerRuntimeException(
                command: "list -a --format json",
                exitCode: result.ExitCode,
                message: "コンテナ一覧の解析に失敗しました。",
                innerException: ex);
        }

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

        List<ImageListItemDto>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<ImageListItemDto>>(result.StandardOutput);
        }
        catch (JsonException ex)
        {
            throw new ContainerRuntimeException(
                command: "image list --format json --no-trunc",
                exitCode: result.ExitCode,
                message: "コンテナーイメージ一覧の解析に失敗しました。",
                innerException: ex);
        }

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

        List<ContainerInspectDto>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<ContainerInspectDto>>(result.StandardOutput);
        }
        catch (JsonException ex)
        {
            throw new ContainerRuntimeException(
                command: $"container inspect {containerId}",
                exitCode: result.ExitCode,
                message: "コンテナ詳細情報の解析に失敗しました。",
                innerException: ex);
        }

        var item = items?.FirstOrDefault();
        if (item is null)
        {
            throw new ContainerRuntimeException($"container inspect {containerId}", result.ExitCode, "コンテナ詳細情報が見つかりませんでした。");
        }

        return MapInspectDetail(item);
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
