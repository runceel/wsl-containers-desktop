# Presentation層: ボリューム一覧ViewModelの状態管理

> このドキュメントは現時点のスナップショットです。経緯・検討過程は書きません。

## 概要

`VolumesViewModel`（`ViewModels/VolumesViewModel.cs`）は、ローカルボリューム一覧の取得、新規作成、
不要ボリュームの削除を担うViewModel。Application層の`IVolumeManagementService`にのみ依存し、
`wslc` CLIの具体的な呼び出し方法には依存しない。

## 一覧UI

- `VolumesPage`はローカルボリューム一覧を表示する。各行は`VolumeRowViewModel`で、ボリューム名、
  ドライバー、作成日時、使用状況、削除操作を表示する。
- 表形式の`ListView`は`TableListViewItemStyle`で行コンテンツを横方向いっぱいに広げ、ヘッダーGridと
  行Gridの水平paddingを揃える。
- 使用状況は、参照中コンテナ名がある場合はコンテナ名をカンマ区切りで表示し、参照がない場合は
  `Unused`として表示する。
- 参照中ボリュームの削除ボタンは無効化する。
- `Volumes`が空の場合は空状態テキストを表示する。

## 更新とエラー表示

- `RefreshAsync`は`IVolumeManagementService.GetVolumesAsync`で最新状態を取得し、成功時に`Volumes`を
  差分更新する（`ObservableCollectionReconciler.Reconcile`。`Clear()` を使わず既存の行インスタンスを
  維持し、追加・削除・並び替えのみを適用するため `ListView` の Reset を避けられる。詳細は
  [ADR-0013](../adr/0013-adopt-differential-updates-for-list-views.md)）。行キーは表示・使用する
  フィールドを連結した構造的な文字列キー（`Name ∣ Driver ∣ CreatedAt ∣ 参照コンテナ名の連結`）で、
  内容が変われば別キーとして当該行のみ作り直す。`record` の自動等価は `IReadOnlyList<string>` を
  参照比較するため、ドメインレコードを直接キーにはしない。
- 更新失敗時は既存の一覧を保持し、`ErrorMessage`に例外メッセージを設定する。
- 新しい操作を開始する際は古い成功メッセージをクリアし、エラーと成功メッセージが矛盾して同時表示されない
  ようにする。

## 作成操作

- `NewVolumeName`はユーザーが入力したボリューム名を保持する。`VolumesPage`のTextBoxは
  `Mode=TwoWay, UpdateSourceTrigger=PropertyChanged`でバインドする。
- `NewVolumeName`が空または空白だけの場合、`CreateAsync`はApplication層を呼び出さず、入力必須エラーを
  `ErrorMessage`として表示する。
- `CreateAsync`は開始時に`IsCreating`を`true`にし、入力欄とCreateボタンを無効化し、
  `ProgressRing`を表示する。
- 作成成功後は`NewVolumeName`を空にし、成功メッセージを表示してから一覧を再取得する。
- 作成失敗時は入力値と既存一覧を保持し、`ErrorMessage`に失敗理由を表示する。

## 削除操作

- `VolumesPage`の削除ボタンは、`ContentDialog`で確認を取ってから`VolumesViewModel.DeleteCommand`を実行する。
- `DeleteAsync`は対象行の`IsBusy`を`true`にして二重操作を防ぎ、成功時はライブの`Volumes`から対象行を
  取り除く。
- `VolumeRowViewModel`が参照中として保持しているボリュームに対してはApplication層を呼び出さず、
  参照中コンテナ名を含むエラーを表示する。
- 削除直前のApplication層再評価で参照中と判定された場合は`VolumeInUseException`を捕捉し、
  例外が保持する参照中コンテナ名をエラーとして表示する。
- 削除は`wslc volume remove <name>`に委譲され、強制削除フラグは付けない。ランタイム不調や
  Application層で推定できない参照中ボリュームの拒否は例外としてPresentation層へ伝播し、
  `ErrorMessage`に表示する。
- 削除失敗時は対象行を一覧に残し、`IsBusy`を解除する。
