using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;
using WslContainersDesktop.Infrastructure.Cli;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc</c> CLIを利用してコンテナ統計ポートを実装する。
/// </summary>
public sealed class WslcCliContainerStatsClient(IWslcCliRunner cliRunner) : IContainerStatsClient
{
    private readonly WslcCliCommandExecutor _executor = new(cliRunner);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ContainerResourceUsage>> GetContainerStatsAsync(CancellationToken cancellationToken = default)
    {
        var result = await _executor.RunAsync(["stats", "--format", "json"], cancellationToken);

        var dtos = ContainerStatsParser.ParseStatsOutput(result);
        return dtos
            .Select(dto =>
            {
                var (usage, limit) = ContainerStatsParser.ParseMemoryUsage(dto.MemUsage);
                return new ContainerResourceUsage(
                    ContainerId: dto.ID,
                    Name: dto.Name,
                    CpuPercentage: ContainerStatsParser.ParseCpuPercentage(dto.CPUPerc),
                    MemoryUsageBytes: usage,
                    MemoryLimitBytes: limit);
            })
            .ToList();
    }
}
