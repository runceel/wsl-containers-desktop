using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Ports;

/// <summary>
/// コンテナーイメージ管理ユースケースを提供するインバウンドポート。
/// </summary>
public interface IImageManagementService
{
    /// <summary>
    /// 現在存在するすべてのコンテナーイメージを取得する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<IReadOnlyList<ContainerImage>> GetImagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したイメージ参照を取得する。
    /// </summary>
    /// <param name="imageReference">取得するイメージ参照。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task PullAsync(string imageReference, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したコンテナーイメージを削除する。
    /// </summary>
    /// <param name="imageId">対象イメージのID。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task DeleteAsync(string imageId, CancellationToken cancellationToken = default);
}
