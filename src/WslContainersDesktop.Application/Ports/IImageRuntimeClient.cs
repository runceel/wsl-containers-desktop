using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Ports;

/// <summary>
/// イメージランタイム操作を抽象化するアウトバウンドポート。
/// Infrastructure層で実装される。
/// </summary>
public interface IImageRuntimeClient
{
    /// <summary>
    /// 現在存在するすべてのコンテナーイメージを取得する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<IReadOnlyList<ContainerImage>> ListImagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したイメージ参照を取得する。
    /// </summary>
    /// <param name="imageReference">取得するイメージ参照。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task PullImageAsync(string imageReference, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したコンテナーイメージを削除する。
    /// </summary>
    /// <param name="imageId">対象イメージのID。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task DeleteImageAsync(string imageId, CancellationToken cancellationToken = default);
}
