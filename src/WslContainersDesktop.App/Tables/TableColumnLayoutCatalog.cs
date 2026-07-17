using System;
using Microsoft.UI.Xaml;

namespace WslContainersDesktop_App.Tables;

/// <summary>
/// Creates table column layouts for the supported presets.
/// </summary>
internal static class TableColumnLayoutCatalog
{
    /// <summary>
    /// Resource key for the container table column layout.
    /// </summary>
    internal const string ContainersResourceKey = "ContainersTableColumnLayout";

    /// <summary>
    /// Resource key for the image table column layout.
    /// </summary>
    internal const string ImagesResourceKey = "ImagesTableColumnLayout";

    /// <summary>
    /// Resource key for the volume table column layout.
    /// </summary>
    internal const string VolumesResourceKey = "VolumesTableColumnLayout";

    /// <summary>
    /// Resource key for the network table column layout.
    /// </summary>
    internal const string NetworksResourceKey = "NetworksTableColumnLayout";

    /// <summary>
    /// Resource key for the dashboard statistics table column layout.
    /// </summary>
    internal const string DashboardStatsResourceKey = "DashboardStatsTableColumnLayout";

    /// <summary>
    /// Registers the built-in table column layouts into the specified resource dictionary.
    /// </summary>
    /// <param name="resources">The resource dictionary to populate.</param>
    internal static void RegisterResources(ResourceDictionary resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        RegisterResourceIfMissing(resources, ContainersResourceKey, TableLayoutPreset.Containers);
        RegisterResourceIfMissing(resources, ImagesResourceKey, TableLayoutPreset.Images);
        RegisterResourceIfMissing(resources, VolumesResourceKey, TableLayoutPreset.Volumes);
        RegisterResourceIfMissing(resources, NetworksResourceKey, TableLayoutPreset.Networks);
        RegisterResourceIfMissing(resources, DashboardStatsResourceKey, TableLayoutPreset.DashboardStats);
    }

    private static void RegisterResourceIfMissing(
        ResourceDictionary resources,
        string resourceKey,
        TableLayoutPreset preset)
    {
        if (!resources.ContainsKey(resourceKey))
        {
            resources[resourceKey] = Create(preset);
        }
    }

    /// <summary>
    /// Creates a layout for the specified preset.
    /// </summary>
    /// <param name="preset">The preset to create.</param>
    /// <returns>A new table column layout instance.</returns>
    internal static TableColumnLayout Create(TableLayoutPreset preset)
    {
        return preset switch
        {
            TableLayoutPreset.Containers => new TableColumnLayout(
                4,
                [
                    new GridLength(1d, GridUnitType.Star),
                    new GridLength(130d, GridUnitType.Pixel),
                    new GridLength(120d, GridUnitType.Pixel),
                    new GridLength(150d, GridUnitType.Pixel),
                ],
                [120d, 100d, 96d, 120d],
                [double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity],
                96d),
            TableLayoutPreset.Images => new TableColumnLayout(
                4,
                [
                    new GridLength(2d, GridUnitType.Star),
                    new GridLength(2d, GridUnitType.Star),
                    new GridLength(1d, GridUnitType.Star),
                    new GridLength(1d, GridUnitType.Star),
                ],
                [140d, 160d, 80d, 120d],
                [double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity],
                224d),
            TableLayoutPreset.Volumes => new TableColumnLayout(
                4,
                [
                    new GridLength(220d, GridUnitType.Pixel),
                    new GridLength(120d, GridUnitType.Pixel),
                    new GridLength(180d, GridUnitType.Pixel),
                    new GridLength(1d, GridUnitType.Star),
                ],
                [140d, 96d, 120d, 140d],
                [double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity],
                120d),
            TableLayoutPreset.Networks => new TableColumnLayout(
                6,
                [
                    new GridLength(180d, GridUnitType.Pixel),
                    new GridLength(120d, GridUnitType.Pixel),
                    new GridLength(180d, GridUnitType.Pixel),
                    new GridLength(96d, GridUnitType.Pixel),
                    new GridLength(120d, GridUnitType.Pixel),
                    new GridLength(1d, GridUnitType.Star),
                ],
                [140d, 96d, 120d, 80d, 96d, 140d],
                [double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity],
                120d),
            TableLayoutPreset.DashboardStats => new TableColumnLayout(
                3,
                [
                    new GridLength(1d, GridUnitType.Star),
                    new GridLength(120d, GridUnitType.Pixel),
                    new GridLength(200d, GridUnitType.Pixel),
                ],
                [120d, 80d, 140d],
                [double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity],
                192d),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
        };
    }
}
