using System.Globalization;
using System.Text.Json;
using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Infrastructure.Cli;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc stats --format json</c> の出力を <see cref="ContainerStatsDto"/> の一覧へ
/// パースし、CPU使用率・メモリ使用量の文字列表現を数値へ変換する。
/// </summary>
internal static class ContainerStatsParser
{
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

    // 実機 wslc 2.9.3.0 `stats --format json` は JSON 配列を返す（Phase 5 確認）。
    // 将来のフォーマット差異に備え NDJSON 形式にもフォールバックする。
    public static List<ContainerStatsDto> ParseStatsOutput(CliResult result)
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

    public static double ParseCpuPercentage(string raw)
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

    public static (long Usage, long Limit) ParseMemoryUsage(string raw)
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
}
