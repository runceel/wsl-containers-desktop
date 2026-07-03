// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop_App.ViewModels;

/// <summary>
/// XAMLバインディング用に <see cref="Container"/> をラップする行ViewModel。
/// </summary>
public sealed partial class ContainerRowViewModel : ObservableObject
{
    /// <summary>
    /// <see cref="ContainerRowViewModel"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="container">初期状態の元となるコンテナ。</param>
    public ContainerRowViewModel(Container container)
    {
        Id = container.Id;
        Name = container.Name;
        Image = container.Image;
        CreatedAt = container.CreatedAt;
        State = container.State;
    }

    public string Id { get; }

    public string Name { get; }

    public string Image { get; }

    public DateTimeOffset CreatedAt { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    [NotifyPropertyChangedFor(nameof(CanRestart))]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    public partial ContainerState State { get; set; }

    /// <summary>
    /// この行に対する操作（起動・停止・再起動・削除）が進行中かどうか。
    /// <see langword="true"/> の間は二重操作を防ぐため <see cref="CanStart"/>・<see cref="CanStop"/>・
    /// <see cref="CanRestart"/>・<see cref="CanDelete"/> がすべて <see langword="false"/> になる。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    [NotifyPropertyChangedFor(nameof(CanRestart))]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    public partial bool IsBusy { get; set; } = false;

    /// <summary>
    /// 「起動」操作を適用できるかどうか。
    /// </summary>
    public bool CanStart => State == ContainerState.Stopped && !IsBusy;

    /// <summary>
    /// 「停止」操作を適用できるかどうか。
    /// </summary>
    public bool CanStop => State == ContainerState.Running && !IsBusy;

    /// <summary>
    /// 「再起動」操作を適用できるかどうか。
    /// </summary>
    public bool CanRestart => State == ContainerState.Running && !IsBusy;

    /// <summary>
    /// 「削除」操作を適用できるかどうか。
    /// </summary>
    public bool CanDelete => State == ContainerState.Stopped && !IsBusy;

    /// <summary>
    /// 操作結果として得られた最新の <see cref="Container"/> の状態をこの行に反映する。
    /// </summary>
    /// <param name="container">反映元のコンテナ。</param>
    public void ApplyFrom(Container container)
    {
        State = container.State;
    }
}
