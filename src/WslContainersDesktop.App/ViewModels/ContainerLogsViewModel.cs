// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;
using WslContainersDesktop_App.Collections;

namespace WslContainersDesktop_App.ViewModels;

/// <summary>
/// コンテナログのフォーカス対象となるViewModel。
/// </summary>
public sealed partial class ContainerLogsViewModel : ObservableObject
{
    private const string StoppedLogStatusMessage = "This container is stopped. Existing logs are displayed, but no live log follow is active.";
    private const string MissingLogStatusMessage = "The selected container does not exist.";

    private readonly IContainerManagementService _containerManagementService;
    private readonly IUiDispatcher _uiDispatcher;

    /// <summary>
    /// 表示・一時停止バッファに保持する要素数の上限。
    /// 0以下は無制限ではなく設定ミスとみなし、コンストラクターで直ちに検出する。
    /// </summary>
    private readonly int _capacity;

    private readonly List<string> _pausedBuffer = [];
    private readonly Queue<string> _pendingLines = [];
    private readonly object _queueLock = new();

    private int _dispatchGeneration;
    private int _dispatchPendingToken;

    private CancellationTokenSource? _followCancellation;
    private Task? _followTask;

    /// <summary>
    /// <see cref="ContainerLogsViewModel"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="containerManagementService">コンテナ操作を行うApplication層のサービス。</param>
    /// <param name="uiDispatcher">UIスレッドへディスパッチする実装。省略時は即時実行する。</param>
    /// <param name="capacity">表示・一時停止バッファに保持する要素数の上限。</param>
    public ContainerLogsViewModel(
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
    /// 現在表示中のログ行。
    /// </summary>
    public ObservableCollection<string> LogLines { get; } = [];

    /// <summary>
    /// ログパネルが表示されているかどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsLogPanelVisible { get; private set; }

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
    /// 指定したコンテナのログを開く。
    /// </summary>
    /// <param name="containerId">対象コンテナID。</param>
    public async Task OpenAsync(string containerId)
    {
        await StopFollowAsync();
        ResetPanelForOpen();

        Container? container;
        try
        {
            var containers = await _containerManagementService.GetContainersAsync();
            container = containers.FirstOrDefault(c => c.Id == containerId);
            if (container is null)
            {
                ShowMissingStatus();
                return;
            }

            var logs = await _containerManagementService.GetContainerLogsAsync(containerId);
            AppendDisplayedLines(logs);

            ClearStatus();
            UpdateEmpty();
        }
        catch (ContainerNotFoundException)
        {
            ShowMissingStatus();
            return;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            return;
        }

        if (container.State == ContainerState.Running)
        {
            StartFollow(containerId);
            await Task.Yield();
        }
        else
        {
            LogStatusMessage = StoppedLogStatusMessage;
        }
    }

    /// <summary>
    /// ログ追跡と表示状態を維持したままログパネルを非表示にする。
    /// </summary>
    public void HidePanel()
    {
        IsLogPanelVisible = false;
    }

    /// <summary>
    /// ログの画面反映を一時停止する。
    /// </summary>
    public Task PauseAsync()
    {
        lock (_queueLock)
        {
            IsLogsPaused = true;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 一時停止中に受信したログを順序通りに反映し、画面反映を再開する。
    /// </summary>
    public Task ResumeAsync()
    {
        int dispatchToken;
        bool hadBufferedLines;
        lock (_queueLock)
        {
            IsLogsPaused = false;
            hadBufferedLines = _pausedBuffer.Count > 0;
            dispatchToken = EnqueuePendingLinesUnderLock(_pausedBuffer);
            _pausedBuffer.Clear();
        }

        if (dispatchToken != 0)
        {
            _uiDispatcher.Invoke(() => FlushPendingLines(dispatchToken));
        }
        else if (!hadBufferedLines)
        {
            UpdateEmpty();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 表示中のログだけをクリアする。
    /// </summary>
    public Task ClearAsync()
    {
        ResetPendingDisplayState();
        LogLines.Clear();
        ClearPausedBuffer();
        UpdateEmpty();
        return Task.CompletedTask;
    }

    /// <summary>
    /// ログパネルを閉じ、追跡中のログ取得を停止する。
    /// </summary>
    public async Task CloseAsync()
    {
        await StopFollowAsync();
        ResetPendingDisplayState();
        IsLogPanelVisible = false;
    }

    /// <summary>
    /// 受信したライブログ行を現在の一時停止状態に応じて反映する。
    /// </summary>
    /// <param name="line">ログ行。</param>
    public Task FollowLiveLine(string line)
    {
        int dispatchToken = 0;
        lock (_queueLock)
        {
            if (IsLogsPaused)
            {
                BoundedCollection.AppendBounded(_pausedBuffer, line, _capacity);
            }
            else
            {
                dispatchToken = EnqueuePendingLineUnderLock(line);
            }
        }

        if (dispatchToken != 0)
        {
            _uiDispatcher.Invoke(() => FlushPendingLines(dispatchToken));
        }

        return Task.CompletedTask;
    }

    private void StartFollow(string containerId)
    {
        _followCancellation = new CancellationTokenSource();
        _followTask = FollowStreamAsync(containerId, _followCancellation.Token);
    }

    private async Task StopFollowAsync()
    {
        if (_followCancellation is null)
        {
            return;
        }

        await _followCancellation.CancelAsync();
        if (_followTask is not null)
        {
            try
            {
                await _followTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _followCancellation.Dispose();
        _followCancellation = null;
        _followTask = null;
    }

    private async Task FollowStreamAsync(string containerId, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var line in _containerManagementService.FollowContainerLogsAsync(containerId, cancellationToken))
            {
                await FollowLiveLine(line);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ContainerNotFoundException)
        {
            _uiDispatcher.Invoke(ShowMissingStatus);
        }
        catch (Exception ex)
        {
            _uiDispatcher.Invoke(() => ShowError(ex.Message));
        }
    }

    private int EnqueuePendingLineUnderLock(string line)
    {
        BoundedCollection.EnqueueBounded(_pendingLines, line, _capacity);
        return ReservePendingDispatchTokenUnderLock();
    }

    private int EnqueuePendingLinesUnderLock(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            BoundedCollection.EnqueueBounded(_pendingLines, line, _capacity);
        }

        return ReservePendingDispatchTokenUnderLock();
    }

    /// <summary>
    /// 呼び出し時点で <see cref="_pendingLines"/> にディスパッチ待ちのトークンが
    /// 存在しなければ新規発行し、既に存在する場合は0（未発行）を返す。
    /// <see cref="_queueLock"/> を保持した状態で呼び出すこと。
    /// </summary>
    private int ReservePendingDispatchTokenUnderLock()
    {
        if (_dispatchPendingToken != 0)
        {
            return 0;
        }

        var dispatchToken = ++_dispatchGeneration;
        _dispatchPendingToken = dispatchToken;
        return dispatchToken;
    }

    private void FlushPendingLines(int dispatchToken)
    {
        List<string> lines;

        lock (_queueLock)
        {
            if (_dispatchPendingToken != dispatchToken)
            {
                // Clear/Close/OpenなどによりResetPendingDisplayStateが実行され、
                // このディスパッチは既に無効化されている（トークンが不一致）。
                // 古いキュー内容を反映して表示を復活させてはならない。
                return;
            }

            if (IsLogsPaused)
            {
                while (_pendingLines.Count > 0)
                {
                    BoundedCollection.AppendBounded(_pausedBuffer, _pendingLines.Dequeue(), _capacity);
                }

                _dispatchPendingToken = 0;
                return;
            }

            lines = [];
            while (_pendingLines.Count > 0)
            {
                lines.Add(_pendingLines.Dequeue());
            }

            _dispatchPendingToken = 0;
        }

        if (lines.Count == 0)
        {
            return;
        }

        foreach (var line in lines)
        {
            AppendDisplayedLine(line);
        }
    }

    private void ResetPanelForOpen()
    {
        lock (_queueLock)
        {
            _pendingLines.Clear();
            _pausedBuffer.Clear();
            _dispatchPendingToken = 0;
            IsLogsPaused = false;
        }

        LogLines.Clear();
        IsLogPanelVisible = true;
        ClearStatus();
        UpdateEmpty();
    }

    private void ResetPendingDisplayState()
    {
        lock (_queueLock)
        {
            _pendingLines.Clear();
            _dispatchPendingToken = 0;
        }
    }

    private void ClearPausedBuffer()
    {
        lock (_queueLock)
        {
            _pausedBuffer.Clear();
        }
    }

    private void ShowMissingStatus()
    {
        SetStatus(MissingLogStatusMessage, isError: false);
        UpdateEmpty();
    }

    private void ShowError(string message)
    {
        SetStatus(message, isError: true);
        UpdateEmpty();
    }

    private void ClearStatus()
    {
        SetStatus(message: null, isError: false);
    }

    private void SetStatus(string? message, bool isError)
    {
        IsLogError = isError;
        LogStatusMessage = message;
    }

    private void AppendDisplayedLines(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            AppendDisplayedLine(line);
        }
    }

    private void AppendDisplayedLine(string line)
    {
        BoundedCollection.AppendBounded(LogLines, line, _capacity);
        UpdateEmpty();
    }

    private void UpdateEmpty()
    {
        IsLogEmpty = LogLines.Count == 0;
    }
}
