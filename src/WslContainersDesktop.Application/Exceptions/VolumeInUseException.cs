namespace WslContainersDesktop.Application.Exceptions;

/// <summary>
/// 参照中のコンテナーが存在するボリュームに対して削除が要求されたことを表す例外。
/// </summary>
public sealed class VolumeInUseException : ContainerManagementException
{
    /// <summary>
    /// <see cref="VolumeInUseException"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="volumeName">削除対象のボリューム名。</param>
    /// <param name="referencingContainerNames">対象ボリュームを参照しているコンテナ名。</param>
    public VolumeInUseException(string volumeName, IReadOnlyList<string> referencingContainerNames)
        : base($"Volume '{volumeName}' is in use by: {string.Join(", ", referencingContainerNames)}")
    {
        VolumeName = volumeName;
        ReferencingContainerNames = referencingContainerNames;
    }

    /// <summary>
    /// 削除対象のボリューム名。
    /// </summary>
    public string VolumeName { get; }

    /// <summary>
    /// 対象ボリュームを参照しているコンテナ名。
    /// </summary>
    public IReadOnlyList<string> ReferencingContainerNames { get; }
}
