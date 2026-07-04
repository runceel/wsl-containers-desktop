using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Ports;

/// <summary>
/// コンテナーボリューム管理ユースケースを提供するインバウンドポート。
/// </summary>
public interface IVolumeManagementService
{
    /// <summary>
    /// 現在存在するすべてのコンテナーボリュームを取得する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<IReadOnlyList<ContainerVolume>> GetVolumesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定した名前のコンテナーボリュームを作成する。
    /// </summary>
    /// <param name="name">作成するボリューム名。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>作成したボリューム。</returns>
    Task<ContainerVolume> CreateAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したコンテナーボリュームを削除する。
    /// </summary>
    /// <param name="name">削除するボリューム名。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task DeleteAsync(string name, CancellationToken cancellationToken = default);
}
