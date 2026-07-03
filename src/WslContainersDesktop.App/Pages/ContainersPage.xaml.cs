// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Specialized;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WslContainersDesktop_App.Scrolling;
using WslContainersDesktop_App.ViewModels;
using Windows.ApplicationModel.Resources;

namespace WslContainersDesktop_App.Pages;

public sealed partial class ContainersPage : Page
{
    private readonly ResourceLoader _resourceLoader = new();

    /// <summary>
    /// ログ一覧(<see cref="LstLogs"/>)のテンプレート内に存在する<see cref="ScrollViewer"/>。
    /// <see cref="LstLogs"/>のLoadedイベントで一度だけ探索してキャッシュする。
    /// </summary>
    private ScrollViewer? _logsScrollViewer;

    /// <summary>
    /// ユーザーが末尾までスクロールしている状態を維持しているかどうか。
    /// <see langword="true"/>の間は新しいログ行が追加されるたびに自動で末尾へスクロールする
    /// （受け入れ基準: 「末尾を表示している間は新しい行が追加されると自動的に末尾までスクロールする」）。
    /// ユーザーが手動で上へスクロールした場合は<see langword="false"/>になり、自動スクロールを止める
    /// （受け入れ基準: 「途中までスクロールして読んでいる間は自動スクロールしない」）。
    /// </summary>
    private bool _isFollowingLogBottom = true;

    /// <summary>
    /// 末尾への自動スクロールが既にディスパッチャーへスケジュール済みかどうか。
    /// 短時間に複数行が追加された場合でも<see cref="ScrollViewer.ChangeView(double?, double?, float?, bool)"/>の
    /// 呼び出しを1回に間引くためのフラグ。
    /// </summary>
    private bool _logsScrollToBottomScheduled;

    public ContainersPage()
    {
        // Frame.Navigate(Type)によるページ遷移はパラメーターレスコンストラクタを要求するため、
        // ADR-0010に基づきCompositionRoot(App)のServiceProvider経由でViewModelを解決する。
        ViewModel = ((App)Application.Current).Services.GetRequiredService<ContainersViewModel>();

        InitializeComponent();

        // LstLogsのLoadedは親(このページ)のLoadedより先に発火することがあるため、
        // コンストラクタ(InitializeComponent直後)で購読しておく必要がある
        // （ContainersPage_Loaded側で購読すると間に合わず、自動スクロールが一切
        // 発火しなくなる不具合を招くため）。
        LstLogs.Loaded += LstLogs_Loaded;

        Loaded += ContainersPage_Loaded;
        Unloaded += ContainersPage_Unloaded;
    }

    public ContainersViewModel ViewModel { get; }

    private async void ContainersPage_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.LogLines.CollectionChanged += LogLines_CollectionChanged;

        // LstLogsのLoadedがこのページのLoadedより先に発火し、既にテンプレートが
        // 適用済みの場合に備えたフォールバック（コンストラクタでの購読だけでは
        // 間に合わないタイミングがあり得るため、未取得ならここでも探索する）。
        if (_logsScrollViewer is null)
        {
            TryAttachLogsScrollViewer();
        }

        // ページ表示時に最新のコンテナ一覧を取得する（受け入れ基準:
        // 「アプリ起動後、コンテナ一覧画面に遷移すると...コンテナがすべて表示される」）。
        await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private void ContainersPage_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.LogLines.CollectionChanged -= LogLines_CollectionChanged;
        LstLogs.Loaded -= LstLogs_Loaded;

        if (_logsScrollViewer is not null)
        {
            _logsScrollViewer.ViewChanged -= LogsScrollViewer_ViewChanged;
            _logsScrollViewer = null;
        }
    }

    /// <summary>
    /// <see cref="LstLogs"/>のテンプレートが適用された後に一度だけ呼ばれる。
    /// テンプレート内の<see cref="ScrollViewer"/>を探索し、スクロール位置の変化を監視できるようにする。
    /// </summary>
    private void LstLogs_Loaded(object sender, RoutedEventArgs e) => TryAttachLogsScrollViewer();

    /// <summary>
    /// <see cref="LstLogs"/>のテンプレート内から<see cref="ScrollViewer"/>を探索し、まだ購読していなければ
    /// <see cref="ScrollViewer.ViewChanged"/>を購読する。複数回呼ばれても安全（探索済みなら何もしない）。
    /// </summary>
    private void TryAttachLogsScrollViewer()
    {
        if (_logsScrollViewer is not null)
        {
            return;
        }

        _logsScrollViewer = FindDescendantScrollViewer(LstLogs);
        if (_logsScrollViewer is not null)
        {
            _logsScrollViewer.ViewChanged += LogsScrollViewer_ViewChanged;
        }
    }

    private void LogsScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_logsScrollViewer is null)
        {
            return;
        }

        _isFollowingLogBottom = ScrollPositionEvaluator.IsAtBottom(
            _logsScrollViewer.VerticalOffset,
            _logsScrollViewer.ViewportHeight,
            _logsScrollViewer.ExtentHeight);
    }

    /// <summary>
    /// <see cref="ContainersViewModel.LogLines"/>の変化に応じて自動スクロールを行う。
    /// 一覧が全クリアされた（ログパネルを開き直した等の）場合は末尾表示状態にリセットしたうえで
    /// スクロールし直す（受け入れ基準: 「ログパネルを開いたとき（既存の履歴がある場合）は末尾から表示する」）。
    /// </summary>
    private void LogLines_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            _isFollowingLogBottom = true;
        }

        if (!_isFollowingLogBottom || _logsScrollToBottomScheduled)
        {
            return;
        }

        // 短時間に複数のCollectionChangedが発生しても、実際のスクロールは1回にまとめる。
        _logsScrollToBottomScheduled = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _logsScrollToBottomScheduled = false;

            // ディスパッチャーへのスケジュール後、実際に実行されるまでの間にユーザーが
            // 手動で上へスクロールして_isFollowingLogBottomがfalseになった場合は、
            // ここで最新の状態を再確認してからスクロールする（末尾追従の意図を守るため）。
            if (_isFollowingLogBottom)
            {
                ScrollLogsToBottom();
            }
        });
    }

    private void ScrollLogsToBottom()
    {
        if (_logsScrollViewer is null)
        {
            return;
        }

        _logsScrollViewer.ChangeView(null, _logsScrollViewer.ExtentHeight, null, disableAnimation: true);
    }

    /// <summary>
    /// ビジュアルツリーを幅優先で辿り、最初に見つかった<see cref="ScrollViewer"/>を返す。
    /// </summary>
    private static ScrollViewer? FindDescendantScrollViewer(DependencyObject root)
    {
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var childCount = VisualTreeHelper.GetChildrenCount(current);
            for (var i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(current, i);
                if (child is ScrollViewer scrollViewer)
                {
                    return scrollViewer;
                }

                queue.Enqueue(child);
            }
        }

        return null;
    }

    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (GetContainerRow(sender) is not { } row)
        {
            return;
        }

        // 削除は取り消せない操作のため、実行前に確認ダイアログを表示する
        // （受け入れ基準: 「誤操作を防ぐ確認の上でコンテナが削除され」）。
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _resourceLoader.GetString("DeleteConfirmationDialog_Title"),
            Content = string.Format(_resourceLoader.GetString("DeleteConfirmationDialog_Message"), row.Name),
            PrimaryButtonText = _resourceLoader.GetString("DeleteConfirmationDialog_PrimaryButtonText"),
            CloseButtonText = _resourceLoader.GetString("DeleteConfirmationDialog_CloseButtonText"),
            DefaultButton = ContentDialogButton.Close,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.DeleteCommand.ExecuteAsync(row);
        }
    }

    private async void BtnLogs_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: string containerId })
        {
            await ViewModel.OpenLogsCommand.ExecuteAsync(containerId);
        }
    }

    private async void MenuStart_Click(object sender, RoutedEventArgs e)
    {
        if (GetContainerRow(sender) is { } row)
        {
            await ViewModel.StartCommand.ExecuteAsync(row);
        }
    }

    private async void MenuStop_Click(object sender, RoutedEventArgs e)
    {
        if (GetContainerRow(sender) is { } row)
        {
            await ViewModel.StopCommand.ExecuteAsync(row);
        }
    }

    private async void MenuRestart_Click(object sender, RoutedEventArgs e)
    {
        if (GetContainerRow(sender) is { } row)
        {
            await ViewModel.RestartCommand.ExecuteAsync(row);
        }
    }

    private static ContainerRowViewModel? GetContainerRow(object sender)
    {
        return sender switch
        {
            MenuFlyoutItem { CommandParameter: ContainerRowViewModel row } => row,
            Button { CommandParameter: ContainerRowViewModel row } => row,
            FrameworkElement { DataContext: ContainerRowViewModel row } => row,
            _ => null,
        };
    }

    /// <summary>
    /// x:Bind用のbool→Visibility変換関数。値がtrueのとき表示する。
    /// </summary>
    public Visibility ToVisibleWhenTrue(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// x:Bind用のbool→Visibility変換関数。値がfalseのとき表示する。
    /// </summary>
    public Visibility ToVisibleWhenFalse(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>
    /// x:Bind用のnull/空判定関数。<see cref="InfoBar.IsOpen"/>の表示制御に使用する。
    /// </summary>
    public bool IsNotNullOrEmpty(string? value) => !string.IsNullOrEmpty(value);

}
