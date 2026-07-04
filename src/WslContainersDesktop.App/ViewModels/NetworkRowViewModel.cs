// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop_App.ViewModels;

/// <summary>
/// XAMLバインディング用に <see cref="ContainerNetworkResource"/> をラップする行ViewModel。
/// </summary>
public sealed partial class NetworkRowViewModel : ObservableObject
{
    /// <summary>
    /// <see cref="NetworkRowViewModel"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="network">初期状態の元となるコンテナーネットワーク。</param>
    public NetworkRowViewModel(ContainerNetworkResource network)
    {
        Name = network.Name;
        Driver = network.Driver;
        CreatedAt = network.CreatedAt;
        ConnectedContainerNames = network.ConnectedContainerNames;
        ConnectedContainerCount = network.ConnectedContainerCount;
        IsSystem = network.IsSystem;
        IsInUse = network.IsInUse;
    }

    /// <summary>
    /// ネットワーク名。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// ネットワークドライバー名。
    /// </summary>
    public string Driver { get; }

    /// <summary>
    /// 作成日時。
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// 作成日時の表示用テキスト。
    /// </summary>
    public string CreatedAtText => CreatedAt == DateTimeOffset.MinValue
        ? "Unknown"
        : CreatedAt.ToString("u", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// このネットワークに接続しているコンテナ名の一覧。
    /// </summary>
    public IReadOnlyList<string> ConnectedContainerNames { get; }

    /// <summary>
    /// このネットワークに接続しているコンテナ数。
    /// </summary>
    public int ConnectedContainerCount { get; }

    /// <summary>
    /// 接続コンテナ数の表示用テキスト。
    /// </summary>
    public string ConnectedContainerCountText => ConnectedContainerCount.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// システムネットワークかどうか。
    /// </summary>
    public bool IsSystem { get; }

    /// <summary>
    /// ネットワークがいずれかのコンテナから使用されているかどうか。
    /// </summary>
    public bool IsInUse { get; }

    /// <summary>
    /// ネットワークを削除できるかどうか。
    /// </summary>
    public bool CanDelete => !IsSystem && !IsInUse && !IsBusy;

    /// <summary>
    /// 使用状況の表示用テキスト。
    /// </summary>
    public string UsageText => IsInUse ? string.Join(", ", ConnectedContainerNames) : "Unused";

    /// <summary>
    /// 種別の表示用テキスト。
    /// </summary>
    public string TypeText => IsSystem ? "System" : "User-created";

    /// <summary>
    /// この行に対する操作が進行中かどうか。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    public partial bool IsBusy { get; set; }
}
