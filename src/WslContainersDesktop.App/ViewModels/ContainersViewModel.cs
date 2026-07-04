// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop_App.ViewModels;

/// <summary>
/// コンテナ一覧の表示・操作を管理するViewModel。
/// </summary>
public sealed partial class ContainersViewModel(IContainerManagementService containerManagementService) : ObservableObject
{
    private const string StoppedLogStatusMessage = "This container is stopped. Existing logs are displayed, but no live log follow is active.";
    private const string MissingLogStatusMessage = "The selected container does not exist.";
    private const string StoppedShellStatusMessage = "This container is stopped. Start it before opening a shell.";
    private const string ShellConnectedStatusMessage = "Shell connected.";
    private const string ShellDisconnectedStatusMessage = "Shell disconnected.";

    private readonly IUiDispatcher _uiDispatcher = new ImmediateUiDispatcher();

    /// <summary>
    /// 実行中の操作が対象としているコンテナIDと、その操作種別の対応。
    /// busy状態・進行中の操作種別は本来 <see cref="ContainerRowViewModel.IsBusy"/>／
    /// <see cref="ContainerRowViewModel.PendingOperation"/> だけで表現できるが、
    /// <see cref="RefreshAsync"/> や操作成功後のベストエフォート再同期（<see cref="TryRefreshSilentlyAsync"/>）
    /// では <see cref="ReplaceContainers"/> によって行インスタンスそのものが作り直されるため、
    /// 行インスタンスに紐付くプロパティだけでは再構築後に状態が失われてしまう。
    /// そこでコンテナID単位で永続する記録をこの辞書に持ち、再構築後の新しい行インスタンスへ
    /// busy状態と操作種別を復元できるようにしている（busy状態と操作種別は必ず一致するため、
    /// 単一の情報源として1つの辞書にまとめている。コンテナIDがキーに存在すること自体がbusy中を表す）。
    /// </summary>
    private readonly Dictionary<string, ContainerRowOperation> _pendingOperations = [];

    /// <summary>
    /// 削除操作が進行中（実行開始からサーバー側の応答待ちの間）のコンテナIDの集合。
    /// 削除はUI上「すでに消えたもの」として楽観的に扱うため、この集合に含まれるコンテナIDは
    /// <see cref="ReplaceContainers"/> による再構築時に一覧へ含めない
    /// （<see cref="RefreshAsync"/> によるユーザー手動更新や、他の行の操作完了に伴う
    /// ベストエフォート再同期でサーバーからまだ削除完了前の状態が返ってきても、
    /// 削除中の行が一覧に再度現れて見えるのを防ぐため）。
    /// </summary>
    private readonly HashSet<string> _pendingDeleteContainerIds = [];

    private readonly List<string> _pausedLogBuffer = [];

    private readonly Dictionary<string, ExecSessionState> _execSessions = [];

    private CancellationTokenSource? _logFollowCancellation;

    private Task? _logFollowTask;

    private ExecSessionState? _currentExecSession;

    public ContainersViewModel(IContainerManagementService containerManagementService, IUiDispatcher uiDispatcher)
        : this(containerManagementService)
    {
        _uiDispatcher = uiDispatcher;
    }

    /// <summary>
    /// 現在表示中のコンテナ一覧。
    /// </summary>
    public ObservableCollection<ContainerRowViewModel> Containers { get; } = [];

    /// <summary>
    /// 選択中コンテナのログ行。
    /// </summary>
    public ObservableCollection<string> LogLines { get; } = [];

    /// <summary>
    /// 現在表示中のシェル出力。
    /// </summary>
    public ObservableCollection<string> ShellOutput { get; } = [];

    /// <summary>
    /// 現在表示中のコンテナ詳細行。
    /// </summary>
    public ObservableCollection<string> DetailLines { get; } = [];

    /// <summary>
    /// 直近の操作で発生したエラーメッセージ。エラーがない場合は <see langword="null"/>。
    /// </summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    /// <summary>
    /// 現在表示中のコンテナ詳細。
    /// </summary>
    [ObservableProperty]
    public partial ContainerDetail? SelectedContainerDetail { get; private set; }

    /// <summary>
    /// 詳細取得で発生したエラーメッセージ。
    /// </summary>
    [ObservableProperty]
    public partial string? DetailErrorMessage { get; private set; }

    /// <summary>
    /// シェル状態メッセージ。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShellStatusVisible))]
    public partial string? ShellStatusMessage { get; private set; }

    /// <summary>
    /// シェル入力欄のテキスト。
    /// </summary>
    [ObservableProperty]
    public partial string ShellCommandText { get; set; } = string.Empty;

    /// <summary>
    /// <see cref="Containers"/> が空かどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>
    /// コンテナ詳細パネルが表示されているかどうか。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSidePanelVisible))]
    public partial bool IsDetailPanelVisible { get; private set; }

    /// <summary>
    /// ログパネルが表示されているかどうか。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSidePanelVisible))]
    public partial bool IsLogPanelVisible { get; private set; }

    /// <summary>
    /// シェルパネルが表示されているかどうか。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSidePanelVisible))]
    public partial bool IsShellPanelVisible { get; private set; }

    /// <summary>
    /// 現在のシェルが接続中かどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsShellConnected { get; private set; }

    /// <summary>
    /// シェル接続または入出力がエラー状態かどうか。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShellStatusVisible))]
    public partial bool IsShellError { get; private set; }

    /// <summary>
    /// 表示可能なログが空かどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsLogEmpty { get; private set; } = true;

    /// <summary>
    /// ログ取得または追跡がエラー状態かどうか。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLogStatusVisible))]
    public partial bool IsLogError { get; private set; }

    /// <summary>
    /// ログの画面反映を一時停止しているかどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsLogsPaused { get; private set; }

    /// <summary>
    /// ログ表示領域に表示する状態メッセージ。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLogStatusVisible))]
    public partial string? LogStatusMessage { get; private set; }

    /// <summary>
    /// エラーではないログ状態メッセージを表示するかどうか。
    /// </summary>
    public bool IsLogStatusVisible => !IsLogError && !string.IsNullOrEmpty(LogStatusMessage);

    /// <summary>
    /// シェル状態メッセージを表示するかどうか。
    /// </summary>
    public bool IsShellStatusVisible => !IsShellError && !string.IsNullOrEmpty(ShellStatusMessage);

    /// <summary>
    /// 右側の補助パネル領域を表示するかどうか。
    /// </summary>
    public bool IsSidePanelVisible => IsDetailPanelVisible || IsShellPanelVisible || IsLogPanelVisible;

    /// <summary>
    /// コンテナ一覧を手動で再取得する。
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var containers = await containerManagementService.GetContainersAsync();
            ReplaceContainers(containers);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            // 失敗時は直前に表示していた一覧を保持したまま、エラーメッセージのみを表示する。
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// 指定したコンテナのログを開く。
    /// </summary>
    /// <param name="containerId">対象コンテナID。</param>
    [RelayCommand]
    public async Task OpenLogsAsync(string containerId)
    {
        await StopLogFollowAsync();
        ResetLogPanelForOpen();

        Container? container;
        try
        {
            var containers = await containerManagementService.GetContainersAsync();
            container = containers.FirstOrDefault(c => c.Id == containerId);
            if (container is null)
            {
                ShowMissingLogStatus();
                return;
            }

            var logs = await containerManagementService.GetContainerLogsAsync(containerId);
            AppendDisplayedLogLines(logs);

            ClearLogStatus();
            UpdateLogEmpty();
        }
        catch (ContainerNotFoundException)
        {
            ShowMissingLogStatus();
            return;
        }
        catch (Exception ex)
        {
            ShowLogError(ex.Message);
            return;
        }

        if (container.State == ContainerState.Running)
        {
            StartLogFollow(containerId);
            await Task.Yield();
        }
        else
        {
            LogStatusMessage = StoppedLogStatusMessage;
        }
    }

    /// <summary>
    /// 指定したコンテナの詳細情報を開く。
    /// </summary>
    /// <param name="containerId">対象コンテナID。</param>
    [RelayCommand]
    public async Task OpenDetailsAsync(string containerId)
    {
        IsDetailPanelVisible = true;
        DetailErrorMessage = null;
        DetailLines.Clear();
        try
        {
            SelectedContainerDetail = await containerManagementService.GetContainerDetailAsync(containerId);
            PopulateDetailLines(SelectedContainerDetail);
        }
        catch (Exception ex)
        {
            DetailErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// 詳細パネルを閉じる。
    /// </summary>
    [RelayCommand]
    public Task CloseDetailsAsync()
    {
        IsDetailPanelVisible = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 指定したコンテナ内にシェルを開く。
    /// </summary>
    /// <param name="containerId">対象コンテナID。</param>
    [RelayCommand]
    public async Task OpenShellAsync(string containerId)
    {
        IsShellPanelVisible = true;

        if (_currentExecSession is not null && _currentExecSession.ContainerId != containerId)
        {
            await CloseShellSessionAsync(_currentExecSession, showDisconnectedStatus: false);
        }

        if (SelectedContainerDetail?.Id == containerId && SelectedContainerDetail.State != ContainerState.Running)
        {
            ShowShellStatus(StoppedShellStatusMessage, isError: true);
            return;
        }

        if (_execSessions.TryGetValue(containerId, out var existing) && !existing.Session.IsClosed)
        {
            UseShellSession(existing);
            return;
        }

        if (existing is not null)
        {
            _execSessions.Remove(containerId);
            await existing.Session.CloseAsync();
            existing.Dispose();
        }

        try
        {
            var session = await containerManagementService.OpenExecSessionAsync(containerId);
            var state = new ExecSessionState(containerId, session);
            _execSessions[containerId] = state;
            UseShellSession(state);
            state.OutputTask = ReadShellOutputAsync(state);
        }
        catch (InvalidContainerOperationException)
        {
            ShowShellStatus(StoppedShellStatusMessage, isError: true);
            IsShellConnected = false;
        }
        catch (Exception ex)
        {
            ShowShellStatus(ex.Message, isError: true);
            IsShellConnected = false;
        }
    }

    /// <summary>
    /// 現在のシェルへ入力欄のコマンドを送信する。
    /// </summary>
    [RelayCommand]
    public async Task SendShellCommandAsync()
    {
        if (_currentExecSession is null || _currentExecSession.Session.IsClosed)
        {
            ShowShellStatus(ShellDisconnectedStatusMessage, isError: true);
            IsShellConnected = false;
            return;
        }

        var command = ShellCommandText.TrimEnd('\r', '\n');
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        try
        {
            await _currentExecSession.Session.SendCommandAsync(command);
            ShellCommandText = string.Empty;
        }
        catch (Exception ex)
        {
            IsShellConnected = false;
            ShowShellStatus(ex.Message, isError: true);
        }
    }

    /// <summary>
    /// 現在のシェルセッションを閉じる。
    /// </summary>
    [RelayCommand]
    public async Task CloseShellAsync()
    {
        if (_currentExecSession is null)
        {
            IsShellPanelVisible = false;
            IsShellConnected = false;
            return;
        }

        await CloseShellSessionAsync(_currentExecSession, showDisconnectedStatus: true);
        IsShellPanelVisible = false;
    }

    /// <summary>
    /// ログの画面反映を一時停止する。
    /// </summary>
    [RelayCommand]
    public Task PauseLogsAsync()
    {
        IsLogsPaused = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 一時停止中に受信したログを順序通りに反映し、画面反映を再開する。
    /// </summary>
    [RelayCommand]
    public Task ResumeLogsAsync()
    {
        IsLogsPaused = false;
        AppendDisplayedLogLines(_pausedLogBuffer);
        _pausedLogBuffer.Clear();
        UpdateLogEmpty();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 表示中のログだけをクリアする。
    /// </summary>
    [RelayCommand]
    public Task ClearLogsAsync()
    {
        LogLines.Clear();
        _pausedLogBuffer.Clear();
        UpdateLogEmpty();
        return Task.CompletedTask;
    }

    /// <summary>
    /// ログパネルを閉じ、追跡中のログ取得を停止する。
    /// </summary>
    [RelayCommand]
    public async Task CloseLogsAsync()
    {
        await StopLogFollowAsync();
        IsLogPanelVisible = false;
    }

    /// <summary>
    /// 受信したライブログ行を現在の一時停止状態に応じて反映する。
    /// </summary>
    /// <param name="line">ログ行。</param>
    public Task FollowLiveLine(string line)
    {
        _uiDispatcher.Invoke(() =>
        {
            if (IsLogsPaused)
            {
                _pausedLogBuffer.Add(line);
            }
            else
            {
                AppendDisplayedLogLine(line);
            }
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// 指定したコンテナを起動する。
    /// </summary>
    /// <param name="row">対象の行。</param>
    [RelayCommand]
    private Task StartAsync(ContainerRowViewModel row)
    {
        return ExecuteRowOperationAsync(row, ContainerRowOperation.Starting, () => containerManagementService.StartAsync(row.Id));
    }

    /// <summary>
    /// 指定したコンテナを停止する。
    /// </summary>
    /// <param name="row">対象の行。</param>
    [RelayCommand]
    private Task StopAsync(ContainerRowViewModel row)
    {
        return ExecuteRowOperationAsync(row, ContainerRowOperation.Stopping, () => containerManagementService.StopAsync(row.Id));
    }

    /// <summary>
    /// 指定したコンテナを再起動する。
    /// </summary>
    /// <param name="row">対象の行。</param>
    [RelayCommand]
    private Task RestartAsync(ContainerRowViewModel row)
    {
        return ExecuteRowOperationAsync(row, ContainerRowOperation.Restarting, () => containerManagementService.RestartAsync(row.Id));
    }

    /// <summary>
    /// 指定したコンテナを削除する。呼び出し前に削除確認はView側で完了している前提。
    /// 実行中は二重操作を防ぐため <see cref="BeginBusy"/> でコンテナIDをbusy状態にし、
    /// 成功・失敗いずれの分岐でも <see cref="EndBusy"/> を呼んでbusy状態を解除する
    /// （呼び出し位置の制約は <see cref="EndBusy"/> のドキュメントを参照）。
    /// 失敗時は、削除自体は例外で終わったにもかかわらず行が一覧から消えたままになる状況
    /// （復旧用リフレッシュも失敗し、かつ他の経路でも行が復元されていない場合）に限り、
    /// 削除開始時に受け取った行を一覧へ戻す（詳細は <see cref="RestoreRowIfDeleteFailedAndMissing"/>
    /// のドキュメントを参照）。
    /// </summary>
    /// <param name="row">対象の行。</param>
    [RelayCommand]
    private async Task DeleteAsync(ContainerRowViewModel row)
    {
        var id = row.Id;
        BeginBusy(id, ContainerRowOperation.Deleting);
        _pendingDeleteContainerIds.Add(id);
        try
        {
            await containerManagementService.DeleteAsync(id);
            EndBusy(id);
            _pendingDeleteContainerIds.Remove(id);

            // 削除は一覧から行を取り除くだけで完結する操作のため、Start/Stop/Restartと異なり
            // バックグラウンドでの全件再同期は行わない（削除後の対象コンテナはサーバー側の
            // 一覧からも消えている前提であり、再同期の必要性が薄いため）。
            // 取り除く行は FindLiveRow で再検索したものを使う（理由は同メソッドのドキュメントを参照）。
            var liveRow = FindLiveRow(id);
            if (liveRow is not null)
            {
                Containers.Remove(liveRow);
            }

            IsEmpty = Containers.Count == 0;
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            // 削除処理自体は例外で失敗しているが、行が実際には削除されている等の
            // 可能性もあるため、ベストエフォートで実際のサーバー状態にUIを合わせ直す。
            _pendingDeleteContainerIds.Remove(id);
            var refreshSucceeded = await HandleOperationFailureAsync(id, ex);
            RestoreRowIfDeleteFailedAndMissing(row, refreshSucceeded);
        }
    }

    /// <summary>
    /// 削除失敗後、行が一覧から消えたままになっている場合に、削除開始時の行を一覧へ戻す。
    /// </summary>
    /// <remarks>
    /// 復旧用リフレッシュ（<see cref="TryRefreshSilentlyAsync"/>）が成功していれば、その時点の
    /// サーバー側の実際の状態が既に <see cref="Containers"/> へ反映されており、対象コンテナが
    /// まだ存在するなら一覧にも含まれているはずなので、このメソッドは何もしない。
    /// 復旧用リフレッシュが失敗した場合のみ、削除中に <see cref="_pendingDeleteContainerIds"/>
    /// によって <see cref="ReplaceContainers"/> から除外され続けていた行が一覧から消えたまま
    /// になっている可能性があるため、<see cref="FindLiveRow"/> で確認したうえで、実際にはサーバー
    /// 側にまだ存在するコンテナとして、削除開始時に捕まえていた行インスタンス（<paramref name="row"/>）
    /// をそのまま一覧へ戻す。
    /// </remarks>
    /// <param name="row">削除開始時に受け取った行インスタンス。</param>
    /// <param name="refreshSucceeded">削除失敗後の復旧用リフレッシュが成功したかどうか。</param>
    private void RestoreRowIfDeleteFailedAndMissing(ContainerRowViewModel row, bool refreshSucceeded)
    {
        if (refreshSucceeded || FindLiveRow(row.Id) is not null)
        {
            return;
        }

        row.IsBusy = false;
        row.PendingOperation = ContainerRowOperation.None;
        Containers.Add(row);
        IsEmpty = false;
    }

    /// <summary>
    /// 指定した行に対する単一コンテナ操作（起動・停止・再起動）を実行する。
    /// 実行中は二重操作を防ぐため <see cref="BeginBusy"/> でコンテナIDをbusy状態にし、
    /// 成功・失敗いずれの分岐でも <see cref="EndBusy"/> を呼んでbusy状態を解除する
    /// （呼び出し位置の制約は <see cref="EndBusy"/> のドキュメントを参照）。
    /// </summary>
    /// <param name="row">対象の行。</param>
    /// <param name="operationKind">State列に表示する進行中の操作種別。</param>
    /// <param name="operation">実行する操作。成功時は更新後の <see cref="Container"/> を返す。</param>
    private async Task ExecuteRowOperationAsync(ContainerRowViewModel row, ContainerRowOperation operationKind, Func<Task<Container>> operation)
    {
        var id = row.Id;
        BeginBusy(id, operationKind);
        try
        {
            var updated = await operation();
            EndBusy(id);

            // 操作成功後は、後続のバックグラウンド再同期を待たずに即座にUIへ反映する
            // （楽観的更新）。詳細設計フェーズのラバーダックレビューで、再同期が失敗した
            // 場合に古い状態へ巻き戻さないための対策として導入した。
            // 反映先は FindLiveRow で再検索した行を使う（理由は同メソッドのドキュメントを参照）。
            var liveRow = FindLiveRow(id);
            liveRow?.ApplyFrom(updated);
            ErrorMessage = null;

            // 楽観的更新の直後にも、他クライアントによる変更等を取り込むためベストエフォートで
            // 一覧全体を再同期する。
            await TryRefreshSilentlyAsync();
        }
        catch (Exception ex)
        {
            // 操作自体は例外で失敗しているが、実際にはサーバー側の状態が変化している
            // 可能性もあるため、ベストエフォートで実際のサーバー状態にUIを合わせ直す。
            // 戻り値（再同期の成否）は使わない。DeleteAsyncと異なり、Start/Stop/Restartの
            // 失敗時には「一覧から消えたままの行を復元する」といった復旧処理が不要なため。
            await HandleOperationFailureAsync(id, ex);
        }
    }

    /// <summary>
    /// 行操作（起動・停止・再起動・削除）が例外で失敗した場合の共通後処理。
    /// <see cref="EndBusy"/> で対象行のbusy状態を解除し、エラーメッセージを設定したうえで、
    /// ベストエフォートの再同期（<see cref="TryRefreshSilentlyAsync"/>）を行う。
    /// <see cref="EndBusy"/> を再同期より前に呼ぶ理由は <see cref="EndBusy"/> 自体のドキュメントを参照。
    /// </summary>
    /// <param name="containerId">対象のコンテナID。</param>
    /// <param name="exception">発生した例外。</param>
    /// <returns>
    /// <see cref="TryRefreshSilentlyAsync"/> の戻り値をそのまま返す。すなわち再同期が成功し
    /// <see cref="Containers"/> がサーバー側の実際の状態に合わせ直された場合は
    /// <see langword="true"/>、再同期にも失敗し何も変更されなかった場合は <see langword="false"/>。
    /// </returns>
    private async Task<bool> HandleOperationFailureAsync(string containerId, Exception exception)
    {
        EndBusy(containerId);
        ErrorMessage = exception.Message;
        return await TryRefreshSilentlyAsync();
    }

    /// <summary>
    /// 直前の操作成功後、ベストエフォートで一覧全体を再同期する。
    /// 失敗した場合は直前に適用した楽観的更新を維持するため、例外を握りつぶす。
    /// </summary>
    /// <returns>
    /// 再同期に成功し <see cref="Containers"/> をサーバー側の実際の状態に置き換えられた場合は
    /// <see langword="true"/>。例外を捕捉した場合は <see langword="false"/> を返し、この場合
    /// <see cref="Containers"/> や <see cref="_busyContainerIds"/> 等の状態は一切変更しない。
    /// </returns>
    private async Task<bool> TryRefreshSilentlyAsync()
    {
        try
        {
            var containers = await containerManagementService.GetContainersAsync();
            ReplaceContainers(containers);
            return true;
        }
        catch
        {
            // ベストエフォートの再同期。失敗してもUIには何も表示しない。
            return false;
        }
    }

    private void StartLogFollow(string containerId)
    {
        _logFollowCancellation = new CancellationTokenSource();
        _logFollowTask = FollowLogStreamAsync(containerId, _logFollowCancellation.Token);
    }

    private void UseShellSession(ExecSessionState state)
    {
        _currentExecSession = state;
        ShellOutput.Clear();
        IsShellConnected = true;
        ShowShellStatus(ShellConnectedStatusMessage, isError: false);
    }

    private async Task CloseShellSessionAsync(ExecSessionState state, bool showDisconnectedStatus)
    {
        await state.Cancellation.CancelAsync();
        await state.Session.CloseAsync();
        if (state.OutputTask is not null)
        {
            try
            {
                await state.OutputTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        state.Dispose();
        _execSessions.Remove(state.ContainerId);
        if (_currentExecSession == state)
        {
            _currentExecSession = null;
        }

        IsShellConnected = false;
        if (showDisconnectedStatus)
        {
            ShowShellStatus(ShellDisconnectedStatusMessage, isError: false);
        }
    }

    private async Task ReadShellOutputAsync(ExecSessionState state)
    {
        try
        {
            await foreach (var chunk in state.Session.ReadOutputAsync(state.Cancellation.Token))
            {
                _uiDispatcher.Invoke(() =>
                {
                    if (_currentExecSession == state)
                    {
                        ShellOutput.Add(chunk);
                    }
                });
            }

            _uiDispatcher.Invoke(() =>
            {
                if (_currentExecSession == state)
                {
                    IsShellConnected = false;
                    ShowShellStatus(ShellDisconnectedStatusMessage, isError: false);
                }
            });
        }
        catch (OperationCanceledException) when (state.Cancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _uiDispatcher.Invoke(() =>
            {
                if (_currentExecSession == state)
                {
                    IsShellConnected = false;
                    ShowShellStatus(ex.Message, isError: true);
                }
            });
        }
    }

    private void ShowShellStatus(string message, bool isError)
    {
        IsShellError = isError;
        ShellStatusMessage = message;
    }

    private void PopulateDetailLines(ContainerDetail detail)
    {
        DetailLines.Add($"ID: {detail.Id}");
        DetailLines.Add($"Name: {detail.Name}");
        DetailLines.Add($"Image: {detail.Image}");
        DetailLines.Add($"State: {detail.State}");
        DetailLines.Add($"Created: {detail.CreatedAt:u}");
        DetailLines.Add($"Command: {detail.Command ?? "(none)"}");
        DetailLines.Add($"Entrypoint: {detail.Entrypoint ?? "(none)"}");
        DetailLines.Add($"Exit code: {detail.RunState.ExitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)"}");
        DetailLines.Add($"Started: {FormatOptionalDateTime(detail.RunState.StartedAt)}");
        DetailLines.Add($"Finished: {FormatOptionalDateTime(detail.RunState.FinishedAt)}");
        DetailLines.Add("Ports:");
        AddIndentedLines(detail.Ports.Select(port =>
        {
            var host = port.HostPort is null ? "(not published)" : $"{port.HostAddress ?? "0.0.0.0"}:{port.HostPort}";
            return $"{host} -> {port.ContainerPort}/{port.Protocol}";
        }));
        DetailLines.Add("Environment:");
        AddIndentedLines(detail.Environment.Select(variable => $"{variable.Name}={variable.Value}"));
        DetailLines.Add("Mounts:");
        AddIndentedLines(detail.Mounts.Select(mount => $"{mount.Type}: {mount.Source} -> {mount.Target} ({(mount.IsReadOnly ? "ro" : "rw")})"));
        DetailLines.Add("Networks:");
        AddIndentedLines(detail.Networks.Select(network => $"{network.Name}: {network.IpAddress ?? "(no IP)"}"));
    }

    private void AddIndentedLines(IEnumerable<string> lines)
    {
        var added = false;
        foreach (var line in lines)
        {
            DetailLines.Add($"  {line}");
            added = true;
        }

        if (!added)
        {
            DetailLines.Add("  (none)");
        }
    }

    private static string FormatOptionalDateTime(DateTimeOffset? value)
    {
        return value?.ToString("u", System.Globalization.CultureInfo.InvariantCulture) ?? "(none)";
    }

    private async Task StopLogFollowAsync()
    {
        if (_logFollowCancellation is null)
        {
            return;
        }

        await _logFollowCancellation.CancelAsync();
        if (_logFollowTask is not null)
        {
            try
            {
                await _logFollowTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _logFollowCancellation.Dispose();
        _logFollowCancellation = null;
        _logFollowTask = null;
    }

    private async Task FollowLogStreamAsync(string containerId, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var line in containerManagementService.FollowContainerLogsAsync(containerId, cancellationToken))
            {
                await FollowLiveLine(line);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ContainerNotFoundException)
        {
            _uiDispatcher.Invoke(ShowMissingLogStatus);
        }
        catch (Exception ex)
        {
            _uiDispatcher.Invoke(() => ShowLogError(ex.Message));
        }
    }

    private void ShowMissingLogStatus()
    {
        SetLogStatus(MissingLogStatusMessage, isError: false);
        UpdateLogEmpty();
    }

    private void ShowLogError(string message)
    {
        SetLogStatus(message, isError: true);
        UpdateLogEmpty();
    }

    private void ResetLogPanelForOpen()
    {
        LogLines.Clear();
        _pausedLogBuffer.Clear();
        IsLogsPaused = false;
        IsLogPanelVisible = true;
        ClearLogStatus();
        UpdateLogEmpty();
    }

    private void ClearLogStatus()
    {
        SetLogStatus(message: null, isError: false);
    }

    private void SetLogStatus(string? message, bool isError)
    {
        IsLogError = isError;
        LogStatusMessage = message;
    }

    private void AppendDisplayedLogLines(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            AppendDisplayedLogLine(line);
        }
    }

    private void AppendDisplayedLogLine(string line)
    {
        LogLines.Add(line);
        UpdateLogEmpty();
    }

    private void UpdateLogEmpty()
    {
        IsLogEmpty = LogLines.Count == 0;
    }

    /// <summary>
    /// 指定したコンテナIDをbusy状態にし、進行中の操作種別を記録する。
    /// <see cref="_pendingOperations"/> にID→操作種別を記録することで、
    /// 操作の完了前に <see cref="ReplaceContainers"/> が呼ばれて行インスタンスが再生成されても
    /// busy状態と操作種別を復元できるようにする。あわせて、その時点でのライブの
    /// <see cref="Containers"/> に該当行が存在すれば、その行の <see cref="ContainerRowViewModel.IsBusy"/>
    /// と <see cref="ContainerRowViewModel.PendingOperation"/> も直接設定する。
    /// </summary>
    /// <param name="containerId">busy状態にするコンテナID。</param>
    /// <param name="operation">進行中の操作種別。</param>
    private void BeginBusy(string containerId, ContainerRowOperation operation)
    {
        _pendingOperations[containerId] = operation;
        var liveRow = FindLiveRow(containerId);
        if (liveRow is not null)
        {
            liveRow.IsBusy = true;
            liveRow.PendingOperation = operation;
        }
    }

    /// <summary>
    /// <see cref="BeginBusy"/> で立てたbusy状態と操作種別を解除する。
    /// </summary>
    /// <remarks>
    /// 呼び出し側は、成功・失敗どちらの分岐であっても、この呼び出しを <c>finally</c> 節ではなく
    /// 各分岐の中で <see cref="TryRefreshSilentlyAsync"/>（内部で行インスタンスを再生成する
    /// <see cref="ReplaceContainers"/> を呼ぶ）より前に行う必要がある。もし <c>finally</c> で
    /// （＝再同期の後で）解除すると、再同期によって作られた新しい行インスタンスは
    /// <see cref="_pendingOperations"/> に残ったままの古い記録を見てbusy状態・操作種別を
    /// 復元してしまい、実際には完了した操作の途中状態表示が解除されなくなる。
    /// </remarks>
    /// <param name="containerId">busy状態を解除するコンテナID。</param>
    private void EndBusy(string containerId)
    {
        _pendingOperations.Remove(containerId);
        var liveRow = FindLiveRow(containerId);
        if (liveRow is not null)
        {
            liveRow.IsBusy = false;
            liveRow.PendingOperation = ContainerRowOperation.None;
        }
    }

    /// <summary>
    /// その時点でのライブの <see cref="Containers"/> から、コンテナIDに一致する行インスタンスを探す。
    /// <see cref="ReplaceContainers"/> によって行インスタンスが再生成された後は、操作開始時に
    /// 捕まえていた行インスタンスがすでにライブのコレクションに存在しない場合があるため、
    /// await をまたいだ後の行操作は必ずこのメソッドで再検索した行に対して行う。
    /// </summary>
    /// <param name="containerId">検索するコンテナID。</param>
    /// <returns>見つかった行。存在しない場合は <see langword="null"/>。</returns>
    private ContainerRowViewModel? FindLiveRow(string containerId) => Containers.FirstOrDefault(r => r.Id == containerId);

    private void ReplaceContainers(IReadOnlyList<Container> containers)
    {
        Containers.Clear();
        foreach (var container in containers)
        {
            if (_pendingDeleteContainerIds.Contains(container.Id))
            {
                // 削除操作が進行中のコンテナは、サーバーからまだ削除完了前の状態が
                // 返ってきていても一覧に含めない（削除の楽観的更新）。
                continue;
            }

            var row = new ContainerRowViewModel(container);
            if (_pendingOperations.TryGetValue(container.Id, out var operation))
            {
                row.IsBusy = true;
                row.PendingOperation = operation;
            }

            Containers.Add(row);
        }

        IsEmpty = Containers.Count == 0;
    }

    private sealed class ExecSessionState(string containerId, IContainerExecSession session) : IDisposable
    {
        public string ContainerId { get; } = containerId;

        public IContainerExecSession Session { get; } = session;

        public CancellationTokenSource Cancellation { get; } = new();

        public Task? OutputTask { get; set; }

        public void Dispose()
        {
            Cancellation.Dispose();
        }
    }
}
