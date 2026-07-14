using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Ports;

/// <summary>
/// ボリュームランタイム操作を抽象化するアウトバウンドポート。
/// Infrastructure層で実装される。
/// </summary>
public interface IVolumeRuntimeClient
{
    /// <summary>
    /// 現在存在するすべてのコンテナーボリュームを取得する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<IReadOnlyList<ContainerVolume>> ListVolumesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定した名前のコンテナーボリュームを作成する。
    /// </summary>
    /// <param name="name">作成するボリューム名。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task CreateVolumeAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したコンテナーボリュームを削除する。
    /// </summary>
    /// <param name="name">削除するボリューム名。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task DeleteVolumeAsync(string name, CancellationToken cancellationToken = default);
}
