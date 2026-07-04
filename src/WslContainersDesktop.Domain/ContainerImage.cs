namespace WslContainersDesktop.Domain;

/// <summary>
/// WSL Containers上のコンテナーイメージを表すエンティティ。
/// </summary>
/// <param name="Id">イメージID。</param>
/// <param name="Repository">リポジトリ名。</param>
/// <param name="Tag">タグ名。</param>
/// <param name="SizeBytes">イメージサイズ（バイト）。</param>
/// <param name="CreatedAt">作成日時。</param>
public sealed record ContainerImage(string Id, string Repository, string Tag, long SizeBytes, DateTimeOffset CreatedAt)
{
    /// <summary>
    /// 一覧表示用のイメージ名。
    /// </summary>
    public string DisplayName => $"{NormalizeNamePart(Repository)}:{NormalizeNamePart(Tag)}";

    private static string NormalizeNamePart(string value) => string.IsNullOrEmpty(value) ? "<none>" : value;
}
