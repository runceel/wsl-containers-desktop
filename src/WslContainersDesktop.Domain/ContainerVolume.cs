namespace WslContainersDesktop.Domain;

/// <summary>
/// WSL Containers上のコンテナーボリュームを表すエンティティ。
/// </summary>
/// <param name="Name">ボリューム名。</param>
/// <param name="Driver">ボリュームドライバー名。</param>
/// <param name="CreatedAt">作成日時。</param>
/// <param name="ReferencingContainerNames">このボリュームを参照しているコンテナ名の一覧。</param>
public sealed record ContainerVolume(
    string Name,
    string Driver,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> ReferencingContainerNames)
{
    /// <summary>
    /// ボリュームがいずれかのコンテナから参照されているかどうか。
    /// </summary>
    public bool IsInUse => ReferencingContainerNames.Count > 0;

    /// <summary>
    /// ボリュームを削除できるかどうか。
    /// </summary>
    public bool CanDelete => !IsInUse;
}
