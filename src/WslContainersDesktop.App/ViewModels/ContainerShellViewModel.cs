// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;
using WslContainersDesktop_App.Collections;

namespace WslContainersDesktop_App.ViewModels;

/// <summary>
/// コンテナシェルのフォーカス対象となるViewModel。
/// </summary>
public sealed partial class ContainerShellViewModel : ObservableObject
{
    private const string StoppedShellStatusMessage = "This container is stopped. Start it before opening a shell.";
    private const string ShellConnectedStatusMessage = "Shell connected.";
    private const string ShellDisconnectedStatusMessage = "Shell disconnected.";

    private readonly IContainerManagementService _containerManagementService;
    private readonly IUiDispatcher _uiDispatcher;

    /// <summary>
    /// シェル出力・一時停止バッファに保持する要素数の上限。
    /// 0以下は無制限ではなく設定ミスとみなし、コンストラクターで直ちに検出する。
    /// </summary>
    private readonly int _capacity;

    private readonly Queue<(ExecSessionState State, string Chunk)> _pendingShellChunks = [];
    private readonly object _shellQueueLock = new();

    private readonly Dictionary<string, ExecSessionState> _execSessions = [];

    private int _shellDispatchGeneration;
    private int _shellDispatchPendingToken;

    private ExecSessionState? _currentExecSession;

    /// <summary>
    /// <see cref="ContainerShellViewModel"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="containerManagementService">コンテナ操作を行うApplication層のサービス。</param>
    /// <param name="uiDispatcher">UIスレッドへディスパッチする実装。省略時は即時実行する。</param>
    /// <param name="capacity">シェル出力・一時停止バッファに保持する要素数の上限。</param>
    public ContainerShellViewModel(
        IContainerManagementService containerManagementService,
        IUiDispatcher? uiDispatcher = null,
        int capacity = 5000)
    {
        _containerManagementService = containerManagementService ?? throw new ArgumentNullException(nameof(containerManagementService));
        _uiDispatcher = uiDispatcher ?? new ImmediateUiDispatcher();
        _capacity = capacity > 0
            ? capacity
            : throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must be a positive number.");
    }

    /// <summary>
    /// 現在表示中のシェル出力。
    /// </summary>
    public ObservableCollection<string> ShellOutput { get; } = [];

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
    /// シェルパネルが表示されているかどうか。
    /// </summary>
    [ObservableProperty]
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
    /// シェル状態メッセージを表示するかどうか。
    /// </summary>
    public bool IsShellStatusVisible => !IsShellError && !string.IsNullOrEmpty(ShellStatusMessage);

    /// <summary>
    /// 指定したコンテナのシェルを開く。
    /// </summary>
    /// <param name="containerId">対象コンテナID。</param>
    /// <param name="knownDetail">開く前に把握しているコンテナ詳細。停止中コンテナの判定に使用する。</param>
    public async Task OpenAsync(string containerId, ContainerDetail? knownDetail)
    {
        IsShellPanelVisible = true;

        if (_currentExecSession is not null && _currentExecSession.ContainerId != containerId)
        {
            await CloseShellSessionAsync(_currentExecSession, showDisconnectedStatus: false);
        }

        if (knownDetail?.Id == containerId && knownDetail.State != ContainerState.Running)
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
            var session = await _containerManagementService.OpenExecSessionAsync(containerId);
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
    /// execセッションと入出力状態を維持したままシェルパネルを非表示にする。
    /// </summary>
    public void HidePanel()
    {
        IsShellPanelVisible = false;
    }

    /// <summary>
    /// 現在のシェルへ入力欄のコマンドを送信する。
    /// </summary>
    public async Task SendAsync()
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
    /// 表示中のシェルを閉じる。
    /// </summary>
    public async Task CloseAsync()
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

    private void UseShellSession(ExecSessionState state)
    {
        ResetPendingShellDisplayState();
        _currentExecSession = state;
        ShellOutput.Clear();
        IsShellConnected = true;
        ShowShellStatus(ShellConnectedStatusMessage, isError: false);
    }

    private async Task CloseShellSessionAsync(ExecSessionState state, bool showDisconnectedStatus)
    {
        ResetPendingShellDisplayState();
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
                EnqueueShellChunk(state, chunk);
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

    private void EnqueueShellChunk(ExecSessionState state, string chunk)
    {
        int dispatchToken;
        lock (_shellQueueLock)
        {
            dispatchToken = EnqueuePendingShellChunkUnderLock(state, chunk);
        }

        if (dispatchToken != 0)
        {
            _uiDispatcher.Invoke(() => FlushPendingShellChunks(dispatchToken));
        }
    }

    private int EnqueuePendingShellChunkUnderLock(ExecSessionState state, string chunk)
    {
        BoundedCollection.EnqueueBounded(_pendingShellChunks, (state, chunk), _capacity);

        if (_shellDispatchPendingToken != 0)
        {
            return 0;
        }

        var dispatchToken = ++_shellDispatchGeneration;
        _shellDispatchPendingToken = dispatchToken;
        return dispatchToken;
    }

    private void FlushPendingShellChunks(int dispatchToken)
    {
        List<(ExecSessionState State, string Chunk)> chunks;

        lock (_shellQueueLock)
        {
            if (_shellDispatchPendingToken != dispatchToken)
            {
                // ResetPendingShellDisplayState（Close/Open）によって無効化された
                // 古いディスパッチ。トークン不一致のため、破棄されたチャンクを
                // 画面に復活させない。
                return;
            }

            chunks = [];
            while (_pendingShellChunks.Count > 0)
            {
                chunks.Add(_pendingShellChunks.Dequeue());
            }

            _shellDispatchPendingToken = 0;
        }

        if (chunks.Count == 0)
        {
            return;
        }

        foreach (var (state, chunk) in chunks)
        {
            if (_currentExecSession == state && !state.Session.IsClosed)
            {
                BoundedCollection.AppendBounded(ShellOutput, chunk, _capacity);
            }
        }
    }

    private void ResetPendingShellDisplayState()
    {
        lock (_shellQueueLock)
        {
            _pendingShellChunks.Clear();
            _shellDispatchPendingToken = 0;
        }
    }

    private void ShowShellStatus(string message, bool isError)
    {
        IsShellError = isError;
        ShellStatusMessage = message;
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
