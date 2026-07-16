using System;

namespace WslContainersDesktop_App.Tables;

/// <summary>
/// Identifies the supported table layout presets.
/// </summary>
public enum TableLayoutPreset
{
    /// <summary>
    /// Layout used by container table columns.
    /// </summary>
    Containers,

    /// <summary>
    /// Layout used by image table columns.
    /// </summary>
    Images,

    /// <summary>
    /// Layout used by volume table columns.
    /// </summary>
    Volumes,

    /// <summary>
    /// Layout used by network table columns.
    /// </summary>
    Networks,

    /// <summary>
    /// Layout used by dashboard statistics table columns.
    /// </summary>
    DashboardStats,
}
