// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop_App.ViewModels;

/// <summary>
/// XAMLバインディング用に <see cref="ContainerVolume"/> をラップする行ViewModel。
/// </summary>
public sealed partial class VolumeRowViewModel : ObservableObject
{
    /// <summary>
    /// <see cref="VolumeRowViewModel"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="volume">初期状態の元となるコンテナーボリューム。</param>
    public VolumeRowViewModel(ContainerVolume volume)
    {
        Name = volume.Name;
        Driver = volume.Driver;
        CreatedAt = volume.CreatedAt;
        ReferencingContainerNames = volume.ReferencingContainerNames;
    }

    /// <summary>
    /// ボリューム名。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// ボリュームドライバー名。
    /// </summary>
    public string Driver { get; }

    /// <summary>
    /// 作成日時。
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// このボリュームを参照しているコンテナ名の一覧。
    /// </summary>
    public IReadOnlyList<string> ReferencingContainerNames { get; }

    /// <summary>
    /// ボリュームがいずれかのコンテナから参照されているかどうか。
    /// </summary>
    public bool IsInUse => ReferencingContainerNames.Count > 0;

    /// <summary>
    /// ボリュームを削除できるかどうか。
    /// </summary>
    public bool CanDelete => !IsInUse && !IsBusy;

    /// <summary>
    /// 参照しているコンテナ名の表示用テキスト。
    /// </summary>
    public string UsageText => IsInUse ? string.Join(", ", ReferencingContainerNames) : "Unused";

    /// <summary>
    /// この行に対する操作が進行中かどうか。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    public partial bool IsBusy { get; set; }
}
