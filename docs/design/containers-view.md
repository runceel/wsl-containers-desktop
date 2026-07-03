# Presentation層: コンテナ一覧ViewModelの状態管理

> このドキュメントは現時点のスナップショットです。経緯・検討過程は書きません。

## 概要

`ContainersViewModel`（`ViewModels/ContainersViewModel.cs`）は、コンテナ一覧の取得・起動・停止・
再起動・削除・ログ表示を担うViewModel。各操作は非同期に実行され、`await` 中に一覧全体が再構築されるケース
（ユーザーによる手動更新、他操作完了に伴うバックグラウンド再同期）を考慮した設計になっている。

## 一覧UI

- `ContainersPage`はCommunityToolkitの`DataGrid`でコンテナ一覧を表示する。列ヘッダーを常時表示し、
  `CanUserResizeColumns=true`でユーザーによる列幅変更を有効にする。
- 一覧列は名前、イメージ、状態、作成日時、操作を表示する。状態列は`ContainerState`を表示用テキストへ変換し、
  実行中/停止中などの状態を一覧上で確認できる。
- 行操作は常時表示する`Logs`ボタンと`More`メニューに集約する。`More`メニュー内の起動・停止・再起動・削除は
  対象コンテナの状態に応じて実行可能な項目だけを表示する。

## 行インスタンスの非永続性と再検索

- `Containers`（`ObservableCollection<ContainerRowViewModel>`）は `ReplaceContainers` によって
  丸ごと作り直される。個々の `ContainerRowViewModel` インスタンスは再構築のたびに新しくなり、
  以前のインスタンスは破棄される。
- そのため、非同期操作の開始時にコマンド引数として受け取った行インスタンス（`row`）は、
  `await` の後には既にライブの `Containers` に存在しない可能性がある。この行インスタンスは
  操作対象の `Id` を得る以外の目的で使い続けない。
- `FindLiveRow(string containerId)` が、その時点でのライブな `Containers` からIDで行を
  再検索する唯一の手段。`await` をまたいだ後の状態反映（busy解除、`ApplyFrom` によるプロパティ
  反映、削除時の `Containers.Remove`）は必ずこのメソッドで取得した行に対して行う。

## busy状態の永続化

- 行の `IsBusy` は `ContainerRowViewModel` インスタンスに紐づくプロパティだが、上記の通り
  インスタンスは再構築で失われる。
- `ContainersViewModel` はコンテナID単位でbusy中のIDを `_busyContainerIds`（`HashSet<string>`）
  に保持し、`BeginBusy`/`EndBusy` で追加・削除する。`ReplaceContainers` は新しい行を作る際に
  この集合を参照し、busy中のIDであれば新しい行にも `IsBusy = true` を復元する。
- `EndBusy` は、対応する `TryRefreshSilentlyAsync`（内部で `ReplaceContainers` を呼ぶ）より
  必ず前に呼び出す。順序を守らないと、再同期後の新しい行が古いbusy記録を見て、実際には完了した
  操作のbusy表示を解除できなくなる。

## 削除の楽観的更新とpending管理

- `DeleteAsync` は、Start/Stop/Restartと異なりバックグラウンドでの全件再同期を行わず、
  成功時にその場で対象行を `Containers` から取り除く。
- `DeleteAsync` の実行中（`await` の開始からサーバー応答まで）、対象コンテナIDを
  `_pendingDeleteContainerIds` に記録する。`ReplaceContainers` はこの集合に含まれるIDを
  一覧の再構築から除外するため、削除中に他操作完了に伴うベストエフォート再同期やユーザーの
  手動更新が走っても、サーバーからまだ削除完了前の状態が返り、削除中の行が一覧に再度現れる
  ことを防ぐ。
- `_busyContainerIds` と `_pendingDeleteContainerIds` は役割が異なる別々の集合として管理する。
  前者はボタンの有効/無効制御、後者は一覧からの除外制御であり、削除操作中は両方に同じIDが
  同時に含まれ得る。

## 操作失敗時の復旧

- 各操作（起動・停止・再起動・削除）が例外で失敗した場合は `HandleOperationFailureAsync` に
  処理を委譲する。`EndBusy` でbusy状態を解除し、エラーメッセージを設定したうえで
  `TryRefreshSilentlyAsync` によりベストエフォートで一覧をサーバー側の実際の状態に合わせ直す。
- `TryRefreshSilentlyAsync` は例外を握りつぶすため、再同期に失敗しても直前の楽観的更新
  （またはエラー表示）を維持する。`HandleOperationFailureAsync` はこの再同期の成否を
  `bool` としてそのまま呼び出し元へ返す。

## 削除失敗時の行復元

- `DeleteAsync` は削除中、対象コンテナIDを `_pendingDeleteContainerIds` に記録し、
  `ReplaceContainers` による再構築から除外し続ける（「削除の楽観的更新とpending管理」参照）。
  そのため、削除が例外で失敗し、かつ `HandleOperationFailureAsync` 内の復旧用リフレッシュ
  （`TryRefreshSilentlyAsync`）も失敗した場合、対象の行は一覧から消えたまま復元されない。
- 実際にはサーバー側にまだ存在するコンテナのため、`RestoreRowIfDeleteFailedAndMissing` が
  「復旧用リフレッシュが失敗している」かつ「対象行が一覧に見当たらない（`FindLiveRow` で
  未検出）」の2条件を確認したうえで、削除開始時に受け取った行インスタンスをそのまま
  `Containers` へ戻す。
- 復旧用リフレッシュが成功していれば、その時点のサーバー側の実際の状態が既に一覧へ
  反映されているため、この復元処理は行わない。

## ログパネルの状態管理

- `OpenLogsAsync`は既存の追跡を停止し、`LogLines`、一時停止バッファ、エラー状態、ステータスメッセージを
  初期化してから対象コンテナの既存ログを取得する。
- 既存ログ取得前に対象コンテナの存在を確認する。存在しない、または取得中に
  `ContainerNotFoundException`が発生した場合は、ログパネルを開いたまま「選択中コンテナが存在しない」
  ステータスを表示する。
- 実行中コンテナの場合は既存ログ表示後に`FollowContainerLogsAsync`を開始し、新規ログだけを追跡する。
  停止中コンテナの場合は既存ログのみ表示し、ライブ追跡は開始しない。
- ライブ追跡から到着した行は`IUiDispatcher`経由でUIスレッドへ反映する。実アプリでは
  `DispatcherQueueUiDispatcher`、単体テストでは即時実行または記録用ディスパッチャを使う。
- `PauseLogsAsync`中に到着した行は`_pausedLogBuffer`に保持し、`ResumeLogsAsync`で受信順に
  `LogLines`へ反映する。`ClearLogsAsync`は表示中の行と一時停止バッファを両方クリアするが、
  追跡自体は継続する。
- `CloseLogsAsync`は追跡用`CancellationTokenSource`をキャンセルしてからログパネルを閉じる。
