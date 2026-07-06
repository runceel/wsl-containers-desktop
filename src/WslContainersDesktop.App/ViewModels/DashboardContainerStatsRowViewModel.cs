// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop_App.ViewModels;

/// <summary>
/// ダッシュボードの稼働中コンテナ1件分のリソース使用量表示行。
/// </summary>
public sealed class DashboardContainerStatsRowViewModel(ContainerResourceUsage usage)
{
    private static readonly (string Unit, long Bytes)[] BinaryUnits =
    [
        ("TiB", 1024L * 1024 * 1024 * 1024),
        ("GiB", 1024L * 1024 * 1024),
        ("MiB", 1024L * 1024),
        ("KiB", 1024L),
    ];

    /// <summary>対象コンテナID。</summary>
    public string ContainerId => usage.ContainerId;

    /// <summary>対象コンテナ名。</summary>
    public string Name => usage.Name;

    /// <summary>CPU使用率の表示文字列（例 "12.3 %"）。</summary>
    public string CpuText => string.Format(CultureInfo.InvariantCulture, "{0:0.0} %", usage.CpuPercentage);

    /// <summary>メモリ使用量の表示文字列（例 "512.0 MiB"）。</summary>
    public string MemoryUsageText => Humanize(usage.MemoryUsageBytes);

    /// <summary>メモリ使用量/上限の表示文字列（上限なしは使用量のみ）。</summary>
    public string MemoryText => usage.MemoryLimitBytes > 0
        ? $"{Humanize(usage.MemoryUsageBytes)} / {Humanize(usage.MemoryLimitBytes)}"
        : Humanize(usage.MemoryUsageBytes);

    private static string Humanize(long bytes)
    {
        if (bytes < 1024)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} B", bytes);
        }

        foreach (var (unit, unitBytes) in BinaryUnits)
        {
            if (bytes >= unitBytes)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0:0.0} {1}", bytes / (double)unitBytes, unit);
            }
        }

        return string.Format(CultureInfo.InvariantCulture, "{0} B", bytes);
    }
}
