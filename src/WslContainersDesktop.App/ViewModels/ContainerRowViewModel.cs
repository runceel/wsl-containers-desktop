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
    [NotifyPropertyChangedFor(nameof(DisplayState))]
    [NotifyPropertyChangedFor(nameof(IsStartActionVisible))]
    [NotifyPropertyChangedFor(nameof(IsStopActionVisible))]
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
    /// この行に対して進行中の操作の種別。State列に途中状態（Stopping等）を表示するために使う。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayState))]
    [NotifyPropertyChangedFor(nameof(IsStartActionVisible))]
    [NotifyPropertyChangedFor(nameof(IsStopActionVisible))]
    public partial ContainerRowOperation PendingOperation { get; set; } = ContainerRowOperation.None;

    /// <summary>
    /// State列の表示に使う値。実際の状態と進行中の操作種別を組み合わせる。
    /// </summary>
    public ContainerRowDisplayState DisplayState => new(State, PendingOperation);

    private (bool Start, bool Stop) ActionVisibility => PendingOperation switch
    {
        ContainerRowOperation.Starting => (true, false),
        ContainerRowOperation.Stopping or ContainerRowOperation.Restarting => (false, true),
        _ => (State == ContainerState.Stopped, State == ContainerState.Running),
    };

    /// <summary>
    /// 「起動」操作のアクションボタンを表示するかどうか。
    /// </summary>
    public bool IsStartActionVisible => ActionVisibility.Start;

    /// <summary>
    /// 「停止」操作のアクションボタンを表示するかどうか。
    /// </summary>
    public bool IsStopActionVisible => ActionVisibility.Stop;

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
