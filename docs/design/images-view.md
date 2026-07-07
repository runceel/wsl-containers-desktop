# Presentation層: イメージ一覧ViewModelの状態管理

> このドキュメントは現時点のスナップショットです。経緯・検討過程は書きません。

## 概要

`ImagesViewModel`（`ViewModels/ImagesViewModel.cs`）は、ローカルイメージ一覧の取得、レジストリからの
pull、不要イメージの削除、イメージからのコンテナー起動を担うViewModel。Application層の
`IImageManagementService`と`IContainerManagementService`に依存し、`wslc` CLIの具体的な呼び出し方法には
依存しない。

## 一覧UI

- `ImagesPage`はローカルイメージ一覧を表示する。各行は`ImageRowViewModel`で、表示名、イメージID、
  サイズ（バイト）、作成日時、起動操作、削除操作を表示する。
- 表形式の`ListView`は`TableListViewItemStyle`で行コンテンツを横方向いっぱいに広げ、ヘッダーGridと
  行Gridの水平paddingを揃える。起動/削除のアクション列はヘッダーと行で同じ固定幅を予約する。
- 表示名はDomain層の`ContainerImage.DisplayName`を使う。untaggedイメージは`<none>:<none>`として表示し、
  一覧・削除の対象に含める。
- 作成日時は`DateTimeOffsetToLocalStringConverter`でローカル日時へ変換して表示する。
- `Images`が空の場合は空状態テキストを表示する。

## 更新とエラー表示

- `RefreshAsync`は`IImageManagementService.GetImagesAsync`で最新状態を取得し、成功時に`Images`を
  差分更新する（`ObservableCollectionReconciler.Reconcile`。`Clear()` を使わず既存の行インスタンスを
  維持し、追加・削除・並び替えのみを適用するため `ListView` の Reset を避けられる。詳細は
  [ADR-0013](../adr/0013-adopt-differential-updates-for-list-views.md)）。行キーは表示・使用する
  フィールドを連結した構造的な文字列キー（`Id ∣ Repository ∣ Tag`。イメージIDは内容ダイジェストのため
  サイズ・作成日時はIDに従属する）で、内容が変われば別キーとして当該行のみ作り直す。
- 更新失敗時は既存の一覧を保持し、`ErrorMessage`に例外メッセージを設定する。
- 新しい操作を開始する際は古い成功メッセージをクリアし、エラーと成功メッセージが矛盾して同時表示されない
  ようにする。

## Pull操作

- `PullReference`はユーザーが入力したイメージ参照を保持する。`ImagesPage`のTextBoxは
  `Mode=TwoWay, UpdateSourceTrigger=PropertyChanged`でバインドする。
- `PullReference`が空または空白だけの場合、`PullAsync`はApplication層を呼び出さず、入力必須エラーを
  `ErrorMessage`として表示する。
- `PullAsync`は開始時に`IsPulling`を`true`にし、入力欄とPullボタンを無効化し、`ProgressRing`を表示する。
- pull成功後は`PullReference`を空にし、成功メッセージを表示してからベストエフォートで一覧を再取得する。
  pull自体が成功した後の一覧再取得失敗は`ErrorMessage`として表示し、pullの成功状態は維持する。
- pull失敗時は入力値と既存一覧を保持し、`ErrorMessage`に失敗理由を表示する。

## 削除操作

- `ImagesPage`の削除ボタンは、`ContentDialog`で確認を取ってから`ImagesViewModel.DeleteCommand`を実行する。
- `DeleteAsync`は対象行の`IsBusy`を`true`にして二重操作を防ぎ、成功時はライブの`Images`から対象行を
  取り除く。
- 削除は`wslc image remove <image>`に委譲され、強制削除フラグは付けない。参照中イメージやランタイム不調は
  `IImageManagementService`からの例外としてPresentation層へ伝播し、`ErrorMessage`に表示する。
- 削除失敗時は対象行を一覧に残し、`IsBusy`を解除する。

## イメージからの起動操作

- `ImagesPage`の起動ボタンは、`ContentDialog`でコンテナー名、停止時の自動削除、ポートマッピング、
  環境変数、上書きコマンドを入力してから`ImagesViewModel.RunCommand`を実行する。
- `RunAsync`はタグ付きイメージでは`repository:tag`を起動元イメージ参照として使い、repositoryまたはtagが
  空・`<none>`の場合はイメージIDを使う。
- コンテナー名、ポートマッピング、環境変数、コマンドはViewModelのダイアログ入力プロパティで保持する。
  `RunAsync`は前後の空白を取り除き、CR/LFいずれの改行でも複数行入力を分割し、空行を除いたリストとして
  `ContainerRunRequest`へ渡す。
- 起動中は`IsRunningImage`を`true`にし、`RunCommand`の再実行を抑止する。成功時は`StatusMessage`に
  `Container started.`を表示してRunダイアログ入力をクリアし、失敗時は入力値を保持したまま`ErrorMessage`に
  失敗理由を表示する。
- 新規コンテナー起動は`IContainerManagementService.RunAsync`に委譲する。`ContainerRunRequest`は
  Application層のポート契約に含まれる要求DTOとして`WslContainersDesktop.Application.Ports`に置く。
