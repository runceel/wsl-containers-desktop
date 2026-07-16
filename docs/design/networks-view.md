# Presentation層: ネットワーク一覧ViewModelの状態管理

> このドキュメントは現時点のスナップショットです。経緯・検討過程は書きません。

## 概要

`NetworksViewModel`（`ViewModels/NetworksViewModel.cs`）は、コンテナーネットワーク一覧の取得、
新規作成、不要ネットワークの削除を担うViewModel。Application層の`INetworkManagementService`にのみ
依存し、`wslc` CLIの具体的な呼び出し方法には依存しない。

## 一覧UI

- `NetworksPage`はコンテナーネットワーク一覧を表示する。各行は`NetworkRowViewModel`で、ネットワーク名、
  ドライバー、作成日時、接続コンテナ数、種別、使用状況、削除操作を表示する。
- 作成日時がランタイムから取得できない場合は`Unknown`として表示する。
- 使用状況は、接続中コンテナ名がある場合はコンテナ名をカンマ区切りで表示し、接続がない場合は
  `Unused`として表示する。
- 種別はシステムネットワークを`System`、ユーザー作成ネットワークを`User-created`として表示する。
- 表形式の`ListView`は`TableListViewItemStyle`で行コンテンツを横方向いっぱいに広げ、ヘッダーGridと
  行Gridの水平paddingを揃える。メタデータ領域と幅`120`の削除操作領域を分離し、ヘッダーと行で
  同じ固定幅を予約する。
- Application resourcesの`NetworksTableColumnLayout`をヘッダーと行で共有し、名前・ドライバー・
  作成日時・接続コンテナ数・種別・使用状況の列幅を同期する。`TableColumnSplitter`は右側全列の余力を
  近い順に使って境界を移動し、星幅の使用状況列をリサイズ後も星幅として維持する。変更した幅は
  ページ遷移後も同じプロセス内で維持する。共通の表レイアウト規則は
  [UI / ビジュアルデザイン方針](design-policy.md#表形式一覧)、
  採用判断は[ADR-0018](../adr/0018-adopt-resizable-table-layouts-with-fixed-action-rails.md)を参照。
- システムネットワークまたは接続中ネットワークの削除ボタンは無効化する。
- ネットワークが存在しない場合は空状態テキストを表示する。
- ユーザー作成ネットワークが存在しない場合は、システムネットワークは表示対象だが削除できないことを
  `InfoBar`で表示する。

## 更新とエラー表示

- `RefreshAsync`は`INetworkManagementService.GetNetworksAsync`で最新状態を取得し、成功時に`Networks`を
  差分更新する（`ObservableCollectionReconciler.Reconcile`。`Clear()` を使わず既存の行インスタンスを
  維持し、追加・削除・並び替えのみを適用するため `ListView` の Reset を避けられる。詳細は
  [ADR-0013](../adr/0013-adopt-differential-updates-for-list-views.md)）。行キーは表示・使用する
  フィールドを連結した構造的な文字列キー（`Name ∣ Driver ∣ CreatedAt ∣ IsSystem ∣ 接続コンテナ名の連結`）で、
  内容が変われば別キーとして当該行のみ作り直す。`record` の自動等価は `IReadOnlyList<string>` を
  参照比較するため、ドメインレコードを直接キーにはしない。
- 更新失敗時は既存の一覧を保持し、`ErrorMessage`に例外メッセージを設定する。
- 新しい操作を開始する際は古い成功メッセージをクリアし、エラーと成功メッセージが矛盾して同時表示されない
  ようにする。

## 作成操作

- `NewNetworkName`はユーザーが入力したネットワーク名を保持する。`NetworksPage`のTextBoxは
  `Mode=TwoWay, UpdateSourceTrigger=PropertyChanged`でバインドする。
- `NewNetworkName`が空または空白だけの場合、`CreateAsync`はApplication層を呼び出さず、入力必須エラーを
  `ErrorMessage`として表示する。
- `CreateAsync`は開始時に`IsCreating`を`true`にし、入力欄とCreateボタンを無効化し、
  `ProgressRing`を表示する。
- 作成成功後は`NewNetworkName`を空にし、成功メッセージを表示してから一覧を再取得する。
- 作成失敗時は入力値と既存一覧を保持し、`ErrorMessage`に失敗理由を表示する。

## 削除操作

- `NetworksPage`の削除ボタンは、`ContentDialog`で確認を取ってから`NetworksViewModel.DeleteCommand`を
  実行する。
- `DeleteAsync`は対象行の`IsBusy`を`true`にして二重操作を防ぎ、成功時はライブの`Networks`から対象行を
  取り除く。
- `NetworkRowViewModel`がシステムネットワークとして保持している行に対してはApplication層を呼び出さず、
  削除不可エラーを表示する。
- `NetworkRowViewModel`が接続中として保持している行に対してはApplication層を呼び出さず、接続中
  コンテナ名を含むエラーを表示する。
- 削除直前のApplication層再評価でシステムネットワークと判定された場合は
  `SystemNetworkDeletionException`を捕捉し、例外メッセージを表示する。
- 削除直前のApplication層再評価で接続中と判定された場合は`NetworkInUseException`を捕捉し、
  例外が保持する接続中コンテナ名をエラーとして表示する。
- 削除は`wslc network remove <name>`に委譲され、強制削除フラグは付けない。ランタイム不調や
  Application層で推定できない拒否は例外としてPresentation層へ伝播し、`ErrorMessage`に表示する。
- 削除失敗時は対象行を一覧に残し、`IsBusy`を解除する。
