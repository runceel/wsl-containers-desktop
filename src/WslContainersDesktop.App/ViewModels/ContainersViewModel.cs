// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop_App.ViewModels;

/// <summary>
/// コンテナ一覧・詳細・ログ・シェルの各構成要素をまとめる、XAMLバインディング・コマンド用の
/// ファサードViewModel（<see href="../../docs/adr/0017-split-containersviewmodel-and-runtime-client-into-focused-components.md">ADR-0017</see>）。
/// 一覧の状態・ロジックは <see cref="List"/>（<see cref="ContainerListViewModel"/>）が担い、このクラスは
/// 手書きの委譲プロパティと生成済みコマンドで転送するだけにする。
/// </summary>
public sealed partial class ContainersViewModel : ObservableObject
{
    /// <summary>
    /// フォーカス対象のコンテナ一覧構成要素を表すViewModel。
    /// </summary>
    public ContainerListViewModel List { get; }

    /// <summary>
    /// フォーカス対象のコンテナ詳細構成要素を表すViewModel。
    /// </summary>
    public ContainerDetailsViewModel Details { get; }

    /// <summary>
    /// フォーカス対象のコンテナログ構成要素を表すViewModel。
    /// </summary>
    public ContainerLogsViewModel Logs { get; }

    /// <summary>
    /// フォーカス対象のコンテナシェル構成要素を表すViewModel。
    /// </summary>
    public ContainerShellViewModel Shell { get; }

    /// <summary>
    /// <see cref="ContainersViewModel"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="containerManagementService">コンテナ操作を行うApplication層のサービス。</param>
    /// <param name="uiDispatcher">UIスレッドへディスパッチする実装。省略時は即時実行する。</param>
    /// <param name="capacity">シェル出力・一時停止バッファに保持する要素数の上限。ログ側の同じ上限は<see cref="Logs"/>に渡す。</param>
    public ContainersViewModel(
        IContainerManagementService containerManagementService,
        IUiDispatcher? uiDispatcher = null,
        int capacity = 5000)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must be a positive number.");
        }

        var resolvedUiDispatcher = uiDispatcher ?? new ImmediateUiDispatcher();

        List = new ContainerListViewModel(containerManagementService);
        Details = new ContainerDetailsViewModel(containerManagementService);
        Logs = new ContainerLogsViewModel(containerManagementService, resolvedUiDispatcher, capacity);
        Shell = new ContainerShellViewModel(containerManagementService, resolvedUiDispatcher, capacity);

        // Listの状態変化をこのファサードの同名プロパティ変更として再発行し、x:Bind/INotifyPropertyChanged
        // が既存のプロパティ名（ErrorMessage/IsEmpty）でListの状態を観測できるようにする。
        RelayPropertyChanges(List, nameof(List.ErrorMessage), nameof(List.IsEmpty));

        // Detailsの状態変化をこのファサードの同名プロパティ変更として再発行する。IsDetailPanelVisible
        // はIsSidePanelVisibleの算出にも使われるため、あわせてIsSidePanelVisibleの変更も再発行する。
        RelayPropertyChanges(
            Details,
            [nameof(Details.SelectedContainerDetail), nameof(Details.DetailErrorMessage), nameof(Details.IsDetailPanelVisible)],
            additionalPropertyNames: new Dictionary<string, string[]>
            {
                [nameof(Details.IsDetailPanelVisible)] = [nameof(IsSidePanelVisible)],
            });

        // Logsの状態変化をこのファサードの同名プロパティ変更として再発行する。IsLogPanelVisible
        // はIsSidePanelVisibleの算出にも使われるため、あわせてIsSidePanelVisibleの変更も再発行する。
        RelayPropertyChanges(
            Logs,
            [nameof(Logs.IsLogPanelVisible), nameof(Logs.IsLogEmpty), nameof(Logs.IsLogError), nameof(Logs.IsLogsPaused), nameof(Logs.LogStatusMessage), nameof(Logs.IsLogStatusVisible)],
            additionalPropertyNames: new Dictionary<string, string[]>
            {
                [nameof(Logs.IsLogPanelVisible)] = [nameof(IsSidePanelVisible)],
            });

        // Shellの状態変化をこのファサードの同名プロパティ変更として再発行する。IsShellPanelVisible
        // はIsSidePanelVisibleの算出にも使われるため、あわせてIsSidePanelVisibleの変更も再発行する。
        RelayPropertyChanges(
            Shell,
            [nameof(Shell.ShellStatusMessage), nameof(Shell.ShellCommandText), nameof(Shell.IsShellPanelVisible), nameof(Shell.IsShellConnected), nameof(Shell.IsShellError), nameof(Shell.IsShellStatusVisible)],
            additionalPropertyNames: new Dictionary<string, string[]>
            {
                [nameof(Shell.IsShellPanelVisible)] = [nameof(IsSidePanelVisible)],
            });
    }

    /// <summary>
    /// 現在表示中のコンテナ一覧。<see cref="List"/> が保持するコレクションそのものを返す。
    /// </summary>
    public ObservableCollection<ContainerRowViewModel> Containers => List.Containers;

    /// <summary>
    /// 選択中コンテナのログ行。<see cref="Logs"/> が保持するコレクションそのものを返す。
    /// </summary>
    public ObservableCollection<string> LogLines => Logs.LogLines;

    /// <summary>
    /// 現在表示中のシェル出力。<see cref="Shell"/> が保持するコレクションそのものを返す。
    /// </summary>
    public ObservableCollection<string> ShellOutput => Shell.ShellOutput;

    /// <summary>
    /// 現在表示中のコンテナ詳細行。<see cref="Details"/> が保持するコレクションそのものを返す。
    /// </summary>
    public ObservableCollection<string> DetailLines => Details.DetailLines;

    /// <summary>
    /// 直近の一覧操作で発生したエラーメッセージ。エラーがない場合は <see langword="null"/>。
    /// <see cref="List"/> の同名プロパティをそのまま返す。
    /// </summary>
    public string? ErrorMessage => List.ErrorMessage;

    /// <summary>
    /// 現在表示中のコンテナ詳細。<see cref="Details"/> の同名プロパティをそのまま返す。
    /// </summary>
    public ContainerDetail? SelectedContainerDetail => Details.SelectedContainerDetail;

    /// <summary>
    /// 詳細取得で発生したエラーメッセージ。<see cref="Details"/> の同名プロパティをそのまま返す。
    /// </summary>
    public string? DetailErrorMessage => Details.DetailErrorMessage;

    /// <summary>
    /// シェル状態メッセージ。<see cref="Shell"/> の同名プロパティをそのまま返す。
    /// </summary>
    public string? ShellStatusMessage => Shell.ShellStatusMessage;

    /// <summary>
    /// シェル入力欄のテキスト。<see cref="Shell"/> の同名プロパティへ読み書きを委譲する。
    /// </summary>
    public string ShellCommandText
    {
        get => Shell.ShellCommandText;
        set => Shell.ShellCommandText = value;
    }

    /// <summary>
    /// <see cref="Containers"/> が空かどうか。<see cref="List"/> の同名プロパティをそのまま返す。
    /// </summary>
    public bool IsEmpty => List.IsEmpty;

    /// <summary>
    /// コンテナ詳細パネルが表示されているかどうか。<see cref="Details"/> の同名プロパティをそのまま返す。
    /// </summary>
    public bool IsDetailPanelVisible => Details.IsDetailPanelVisible;

    /// <summary>
    /// ログパネルが表示されているかどうか。<see cref="Logs"/> の同名プロパティをそのまま返す。
    /// </summary>
    public bool IsLogPanelVisible => Logs.IsLogPanelVisible;

    /// <summary>
    /// シェルパネルが表示されているかどうか。<see cref="Shell"/> の同名プロパティをそのまま返す。
    /// </summary>
    public bool IsShellPanelVisible => Shell.IsShellPanelVisible;

    /// <summary>
    /// 現在のシェルが接続中かどうか。<see cref="Shell"/> の同名プロパティをそのまま返す。
    /// </summary>
    public bool IsShellConnected => Shell.IsShellConnected;

    /// <summary>
    /// シェル接続または入出力がエラー状態かどうか。<see cref="Shell"/> の同名プロパティをそのまま返す。
    /// </summary>
    public bool IsShellError => Shell.IsShellError;

    /// <summary>
    /// 表示可能なログが空かどうか。<see cref="Logs"/> の同名プロパティをそのまま返す。
    /// </summary>
    public bool IsLogEmpty => Logs.IsLogEmpty;

    /// <summary>
    /// ログ取得または追跡がエラー状態かどうか。<see cref="Logs"/> の同名プロパティをそのまま返す。
    /// </summary>
    public bool IsLogError => Logs.IsLogError;

    /// <summary>
    /// ログの画面反映を一時停止しているかどうか。<see cref="Logs"/> の同名プロパティをそのまま返す。
    /// </summary>
    public bool IsLogsPaused => Logs.IsLogsPaused;

    /// <summary>
    /// ログ表示領域に表示する状態メッセージ。<see cref="Logs"/> の同名プロパティをそのまま返す。
    /// </summary>
    public string? LogStatusMessage => Logs.LogStatusMessage;

    /// <summary>
    /// エラーではないログ状態メッセージを表示するかどうか。<see cref="Logs"/> の同名プロパティをそのまま返す。
    /// </summary>
    public bool IsLogStatusVisible => Logs.IsLogStatusVisible;

    /// <summary>
    /// シェル状態メッセージを表示するかどうか。<see cref="Shell"/> の同名プロパティをそのまま返す。
    /// </summary>
    public bool IsShellStatusVisible => Shell.IsShellStatusVisible;

    /// <summary>
    /// 右側の補助パネル領域を表示するかどうか。
    /// </summary>
    public bool IsSidePanelVisible => IsDetailPanelVisible || IsShellPanelVisible || IsLogPanelVisible;

    /// <summary>
    /// コンテナ一覧を手動で再取得する。<see cref="List"/> へ委譲する。
    /// </summary>
    [RelayCommand]
    private Task RefreshAsync() => List.RefreshAsync();

    /// <summary>
    /// 指定したコンテナのログを開く。<see cref="Logs"/> へ委譲する。
    /// </summary>
    /// <param name="containerId">対象コンテナID。</param>
    [RelayCommand]
    public Task OpenLogsAsync(string containerId) => Logs.OpenAsync(containerId);

    /// <summary>
    /// 指定したコンテナの詳細情報を開く。<see cref="Details"/> へ委譲する。
    /// </summary>
    /// <param name="containerId">対象コンテナID。</param>
    [RelayCommand]
    public Task OpenDetailsAsync(string containerId) => Details.OpenAsync(containerId);

    /// <summary>
    /// 詳細パネルを閉じる。<see cref="Details"/> へ委譲する。
    /// </summary>
    [RelayCommand]
    public Task CloseDetailsAsync() => Details.CloseAsync();

    /// <summary>
    /// 指定したコンテナ内にシェルを開く。<see cref="Shell"/> へ委譲する。呼び出し時点の
    /// <see cref="SelectedContainerDetail"/> を渡し、停止中コンテナの判定に使わせる。
    /// </summary>
    /// <param name="containerId">対象コンテナID。</param>
    [RelayCommand]
    public Task OpenShellAsync(string containerId) => Shell.OpenAsync(containerId, SelectedContainerDetail);

    /// <summary>
    /// シェルセッションを維持したままシェルパネルを非表示にする。<see cref="Shell"/> へ委譲する。
    /// </summary>
    public void HideShellPanel() => Shell.HidePanel();

    /// <summary>
    /// 現在のシェルへ入力欄のコマンドを送信する。<see cref="Shell"/> へ委譲する。
    /// </summary>
    [RelayCommand]
    public Task SendShellCommandAsync() => Shell.SendAsync();

    /// <summary>
    /// 現在のシェルセッションを閉じる。<see cref="Shell"/> へ委譲する。
    /// </summary>
    [RelayCommand]
    public Task CloseShellAsync() => Shell.CloseAsync();

    /// <summary>
    /// ログの画面反映を一時停止する。<see cref="Logs"/> へ委譲する。
    /// </summary>
    [RelayCommand]
    public Task PauseLogsAsync() => Logs.PauseAsync();

    /// <summary>
    /// 一時停止中に受信したログを順序通りに反映し、画面反映を再開する。<see cref="Logs"/> へ委譲する。
    /// </summary>
    [RelayCommand]
    public Task ResumeLogsAsync() => Logs.ResumeAsync();

    /// <summary>
    /// 表示中のログだけをクリアする。<see cref="Logs"/> へ委譲する。
    /// </summary>
    [RelayCommand]
    public Task ClearLogsAsync() => Logs.ClearAsync();

    /// <summary>
    /// ログ追跡を維持したままログパネルを非表示にする。<see cref="Logs"/> へ委譲する。
    /// </summary>
    public void HideLogPanel() => Logs.HidePanel();

    /// <summary>
    /// ログパネルを閉じ、追跡中のログ取得を停止する。<see cref="Logs"/> へ委譲する。
    /// </summary>
    [RelayCommand]
    public Task CloseLogsAsync() => Logs.CloseAsync();

    /// <summary>
    /// 受信したライブログ行を現在の一時停止状態に応じて反映する。<see cref="Logs"/> へ委譲する。
    /// </summary>
    /// <param name="line">ログ行。</param>
    public Task FollowLiveLine(string line) => Logs.FollowLiveLine(line);

    /// <summary>
    /// 指定したコンテナを起動する。<see cref="List"/> へ委譲する。
    /// </summary>
    /// <param name="row">対象の行。</param>
    [RelayCommand]
    private Task StartAsync(ContainerRowViewModel row) => List.StartAsync(row);

    /// <summary>
    /// 指定したコンテナを停止する。<see cref="List"/> へ委譲する。
    /// </summary>
    /// <param name="row">対象の行。</param>
    [RelayCommand]
    private Task StopAsync(ContainerRowViewModel row) => List.StopAsync(row);

    /// <summary>
    /// 指定したコンテナを再起動する。<see cref="List"/> へ委譲する。
    /// </summary>
    /// <param name="row">対象の行。</param>
    [RelayCommand]
    private Task RestartAsync(ContainerRowViewModel row) => List.RestartAsync(row);

    /// <summary>
    /// 指定したコンテナを削除する。呼び出し前に削除確認はView側で完了している前提。<see cref="List"/> へ委譲する。
    /// </summary>
    /// <param name="row">対象の行。</param>
    [RelayCommand]
    private Task DeleteAsync(ContainerRowViewModel row) => List.DeleteAsync(row);

    /// <summary>
    /// 子コンポーネント（<paramref name="source"/>）が発行する<paramref name="propertyNames"/>の
    /// <see cref="ObservableObject.PropertyChanging"/>/<see cref="ObservableObject.PropertyChanged"/>を、
    /// このファサードの同名プロパティの変更として再発行する。ファサードは子コンポーネントの状態を
    /// 手書きの読み取り専用プロパティで転送するだけで自身のバッキングフィールドを持たないため、
    /// x:Bind/INotifyPropertyChangedへ通知するにはこの再発行が必要になる（<see href="../../docs/adr/0017-split-containersviewmodel-and-runtime-client-into-focused-components.md">ADR-0017</see>）。
    /// 一覧・詳細以外の構成要素（ログ・シェル）を同様にファサード直下のプロパティへ転送する場合にも
    /// 再利用できるよう、汎用的な引数を取る。
    /// </summary>
    /// <param name="source">転送元となる子コンポーネント。</param>
    /// <param name="propertyNames">再発行対象のプロパティ名。ファサード側も同名のプロパティを公開している前提。</param>
    private void RelayPropertyChanges(ObservableObject source, params string[] propertyNames)
        => RelayPropertyChanges(source, propertyNames, additionalPropertyNames: null);

    /// <summary>
    /// <see cref="RelayPropertyChanges(ObservableObject, string[])"/> に加え、再発行対象のプロパティに
    /// 依存してこのファサードだけで算出されるプロパティ（例: <see cref="IsSidePanelVisible"/>）も
    /// あわせて変更通知する。子コンポーネント側は自身の算出プロパティの依存関係までは知らないため、
    /// その依存関係はファサード側のこの呼び出しで表現する。
    /// </summary>
    /// <param name="source">転送元となる子コンポーネント。</param>
    /// <param name="propertyNames">再発行対象のプロパティ名。ファサード側も同名のプロパティを公開している前提。</param>
    /// <param name="additionalPropertyNames">
    /// <paramref name="propertyNames"/> の要素をキーとして、その変更時にあわせて再発行するファサード側の
    /// 算出プロパティ名を値に持つマップ。該当するキーがなければ追加の再発行は行わない。
    /// </param>
    private void RelayPropertyChanges(
        ObservableObject source,
        string[] propertyNames,
        IReadOnlyDictionary<string, string[]>? additionalPropertyNames)
    {
        var relayedPropertyNames = new HashSet<string>(propertyNames, StringComparer.Ordinal);

        source.PropertyChanging += (_, e) =>
        {
            if (e.PropertyName is { } propertyName && relayedPropertyNames.Contains(propertyName))
            {
                OnPropertyChanging(propertyName);
                if (additionalPropertyNames?.TryGetValue(propertyName, out var alsoNotify) == true)
                {
                    foreach (var extraPropertyName in alsoNotify)
                    {
                        OnPropertyChanging(extraPropertyName);
                    }
                }
            }
        };

        source.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is { } propertyName && relayedPropertyNames.Contains(propertyName))
            {
                OnPropertyChanged(propertyName);
                if (additionalPropertyNames?.TryGetValue(propertyName, out var alsoNotify) == true)
                {
                    foreach (var extraPropertyName in alsoNotify)
                    {
                        OnPropertyChanged(extraPropertyName);
                    }
                }
            }
        };
    }
}
