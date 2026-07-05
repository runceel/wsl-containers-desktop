using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Ports;

/// <summary>
/// 設定画面のユースケースを提供するインバウンドポート。WSL連携状態の確認と
/// リソース制限の取得・保存・リセットを担う。
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// WSL Containersとの連携状態（バージョン・可用性・要件充足）を取得する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<WslIntegrationStatus> GetIntegrationStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 現在のリソース制限を取得する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<WslResourceLimits> GetResourceLimitsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// リソース制限を保存する。要件を満たさない場合や値が不正な場合は例外を送出し、ストアは変更しない。
    /// </summary>
    /// <param name="limits">保存するリソース制限。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task SaveResourceLimitsAsync(WslResourceLimits limits, CancellationToken cancellationToken = default);

    /// <summary>
    /// リソース制限をWSLの既定にリセットする。要件を満たさない場合は例外を送出し、ストアは変更しない。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task ResetResourceLimitsAsync(CancellationToken cancellationToken = default);
}
