// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;
using WslContainersDesktop_App.Collections;

namespace WslContainersDesktop_App.ViewModels;

/// <summary>
/// コンテナ一覧のフォーカス対象となるViewModel。
/// </summary>
public sealed partial class ContainerListViewModel(IContainerManagementService containerManagementService) : ObservableObject
{
    private readonly IContainerManagementService _containerManagementService = containerManagementService;

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

    /// <summary>
    /// 現在表示中のコンテナ一覧。
    /// </summary>
    public ObservableCollection<ContainerRowViewModel> Containers { get; } = [];

    /// <summary>
    /// 直近のリフレッシュ・行操作で発生したエラーメッセージ。エラーがない場合は <see langword="null"/>。
    /// </summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    /// <summary>
    /// 現在表示中のコンテナ一覧が空かどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>
    /// コンテナ一覧を最新状態に更新する。
    /// </summary>
    public async Task RefreshAsync()
    {
        try
        {
            var containers = await _containerManagementService.GetContainersAsync();
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
    /// 指定したコンテナを起動する。
    /// </summary>
    /// <param name="row">対象の行。</param>
    public Task StartAsync(ContainerRowViewModel row)
    {
        return ExecuteRowOperationAsync(row, ContainerRowOperation.Starting, () => _containerManagementService.StartAsync(row.Id));
    }

    /// <summary>
    /// 指定したコンテナを停止する。
    /// </summary>
    /// <param name="row">対象の行。</param>
    public Task StopAsync(ContainerRowViewModel row)
    {
        return ExecuteRowOperationAsync(row, ContainerRowOperation.Stopping, () => _containerManagementService.StopAsync(row.Id));
    }

    /// <summary>
    /// 指定したコンテナを再起動する。
    /// </summary>
    /// <param name="row">対象の行。</param>
    public Task RestartAsync(ContainerRowViewModel row)
    {
        return ExecuteRowOperationAsync(row, ContainerRowOperation.Restarting, () => _containerManagementService.RestartAsync(row.Id));
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
    public async Task DeleteAsync(ContainerRowViewModel row)
    {
        var id = row.Id;
        BeginBusy(id, ContainerRowOperation.Deleting);
        _pendingDeleteContainerIds.Add(id);
        try
        {
            await _containerManagementService.DeleteAsync(id);
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
    /// <see cref="Containers"/> や <see cref="_pendingOperations"/> 等の状態は一切変更しない。
    /// </returns>
    private async Task<bool> TryRefreshSilentlyAsync()
    {
        try
        {
            var containers = await _containerManagementService.GetContainersAsync();
            ReplaceContainers(containers);
            return true;
        }
        catch
        {
            // ベストエフォートの再同期。失敗してもUIには何も表示しない。
            return false;
        }
    }

    /// <summary>
    /// 指定したコンテナIDをbusy状態にし、進行中の操作種別を記録する。
    /// あわせて、その時点でのライブの <see cref="Containers"/> に該当行が存在すれば、その行の
    /// <see cref="ContainerRowViewModel.IsBusy"/> と <see cref="ContainerRowViewModel.PendingOperation"/>
    /// も直接設定する。
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
    /// 各分岐の中で <see cref="TryRefreshSilentlyAsync"/>（内部で <see cref="ReplaceContainers"/> を
    /// 呼び、維持・新規どちらの行にも <see cref="ApplyPendingOperation"/> で
    /// <see cref="_pendingOperations"/> の内容を反映する）より前に行う必要がある。もし <c>finally</c> で
    /// （＝再同期の後で）解除すると、再同期時に <see cref="_pendingOperations"/> に残ったままの古い記録を
    /// 見てbusy状態・操作種別が再適用されてしまい、実際には完了した操作の途中状態表示が
    /// 解除されなくなる。
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
    /// 差分更新（<see cref="ReplaceContainers"/>）では行インスタンスはキー一致で基本的に維持されるが、
    /// 操作開始時に捕まえていた行が await をまたいだ後には（キー変化による作り直しや、
    /// 一覧からの一時的な消失などで）ライブのコレクションに存在しない場合があるため、
    /// await をまたいだ後の行操作は必ずこのメソッドで再検索した行に対して行う。
    /// </summary>
    /// <param name="containerId">検索するコンテナID。</param>
    /// <returns>見つかった行。存在しない場合は <see langword="null"/>。</returns>
    private ContainerRowViewModel? FindLiveRow(string containerId) => Containers.FirstOrDefault(r => r.Id == containerId);

    private void ReplaceContainers(IReadOnlyList<Container> containers)
    {
        var source = containers
            .Where(container => !_pendingDeleteContainerIds.Contains(container.Id))
            .ToList();

        ObservableCollectionReconciler.Reconcile(
            Containers,
            source,
            targetKeySelector: static row => BuildContainerKey(row.Id, row.Name, row.Image, row.CreatedAt),
            sourceKeySelector: static container => BuildContainerKey(container.Id, container.Name, container.Image, container.CreatedAt),
            create: CreateContainerRow,
            update: UpdateContainerRow);

        IsEmpty = Containers.Count == 0;
    }

    /// <summary>
    /// 差分更新用の行キーを組み立てる。<see cref="ContainerRowViewModel"/> がインプレース更新する
    /// <see cref="ContainerState"/> は含めず、行が保持する読み取り専用の表示フィールド
    /// （Id / Name / Image / CreatedAt）をすべて含める。State 以外の表示フィールドが変われば
    /// キーが変わり該当行だけが作り直されるため、外部での改名などによる表示の陳腐化を防ぐ。
    /// </summary>
    private static string BuildContainerKey(string id, string name, string image, DateTimeOffset createdAt)
        => string.Join('\u001f', id, name, image, createdAt.UtcTicks.ToString(CultureInfo.InvariantCulture));

    private ContainerRowViewModel CreateContainerRow(Container container)
    {
        var row = new ContainerRowViewModel(container);
        ApplyPendingOperation(row);
        return row;
    }

    private void UpdateContainerRow(ContainerRowViewModel row, Container container)
    {
        row.ApplyFrom(container);
        ApplyPendingOperation(row);
    }

    /// <summary>
    /// <see cref="_pendingOperations"/> を単一の情報源として、行の busy 状態・進行中の操作種別を反映する。
    /// 差分更新（<see cref="ReplaceContainers"/>）で行インスタンスが維持されても新規生成されても、
    /// 進行中の操作があれば復元し、なければ解除することで一貫した表示を保つ。
    /// </summary>
    /// <param name="row">反映先の行。</param>
    private void ApplyPendingOperation(ContainerRowViewModel row)
    {
        if (_pendingOperations.TryGetValue(row.Id, out var operation))
        {
            row.IsBusy = true;
            row.PendingOperation = operation;
        }
        else
        {
            row.IsBusy = false;
            row.PendingOperation = ContainerRowOperation.None;
        }
    }
}
