# 0013. 一覧ViewModelの更新に差分更新（ObservableCollectionReconciler）を採用する

## Status

Accepted

## Context

- [Issue #28 UIブラッシュアップ part2](https://github.com/runceel/wsl-containers-desktop/issues/28)
  で次の2点の改善が求められた。
  1. Containers/Images/Networks/Volumes の各一覧が、更新のたびに全体リフレッシュされる。
  2. コンテナの Stop 実行時に途中状態（Stopping）が一覧に表示されず、Stopped になって初めて表示が変わる。
- 各一覧ViewModelの `Replace*`（`ReplaceContainers` など）は `ObservableCollection.Clear()` の後に
  全件を `Add()` していた。これは `CollectionChanged` の Reset を発生させ、`ListView` が全項目を
  破棄・再生成する。結果として選択状態・スクロール位置が失われ、ちらつきの原因になっていた。
- スコープ2の直接原因は別にあった。`ContainersPage.xaml` の状態列は
  `Text="{x:Bind DisplayState}"`（Mode未指定）で、`x:Bind` の既定は `OneTime` のため、
  `BeginBusy` が設定する途中状態（`PendingOperation=Stopping` 等）が反映されない。全件再構築の
  タイミングで最終状態だけが見えていた。途中状態をその場で反映するには行インスタンスが維持され、
  かつバインドが `OneWay` である必要がある。
- 検討した代替案:
  - **毎回全件 Clear+Add のまま維持**: 実装は単純だが Reset によるちらつき・選択喪失が解消できず、
    途中状態のインプレース反映もできない（今回の要求を満たせない）。
  - **`ObservableCollection` 差し替え／`INotifyCollectionChanged` の独自実装**: WinUI公式の
    MVVMガイドラインが `ObservableCollection` の差し替えを避けるよう求めており、独自実装は
    バグの温床になりやすい。
  - **ドメイン `record` を直接キーにする差分更新**: `record` の自動等価は `IReadOnlyList<string>`
    （`ConnectedContainerNames` 等）を参照比較するため、内容が同じでも別インスタンスなら不一致に
    なり、意図しない行の作り直しが起きる。

## Decision

Presentation層に汎用の差分更新ヘルパー
`ObservableCollectionReconciler.Reconcile(...)`（`WslContainersDesktop.App/Collections/`）を追加し、
Containers/Images/Networks/Volumes の各一覧ViewModelの更新をこれに置き換える。

- リコンサイラはキー一致で既存の行インスタンスを維持し、追加・削除・並び替え・（任意の）
  インプレース更新のみを適用する。`Clear()` を使わないため Reset が発生しない。同一内容の
  再取得では `CollectionChanged` を一切発生させない。ソースキーは一意であることを前提とし、
  重複時は例外を投げる。
- キー選択:
  - **Container**: `Id ∣ Name ∣ Image ∣ CreatedAt`。`State` はキーに含めず、キー一致時に
    `ApplyFrom` でインプレース更新する。これにより状態遷移は同一インスタンスのまま反映され、
    他の表示フィールドが変わった行だけが作り直される。
  - **Image / Network / Volume**: 表示・使用するフィールドを連結した**構造的な文字列キー**を用いる
    （ドメイン `record` を直接キーにはしない）。インプレース更新は行わず、内容が変われば別キーとして
    該当行のみ remove+add で作り直す。
- スコープ2対応として、行インスタンスが維持されることを前提に `ContainersPage.xaml` の状態列を
  `Mode=OneWay` に変更し、`_pendingOperations` を単一の情報源として busy/途中状態を差分更新の
  新規・更新いずれの経路でも復元する。

この方針は Issue #28 以降のすべての一覧ViewModelに適用する。

## Consequences

- 一覧更新時の Reset が解消され、ちらつき・選択喪失・スクロール位置の喪失がなくなる。
- 行インスタンスが維持されるため、`OneWay` バインドと `_pendingOperations` の復元により、
  コンテナ操作の途中状態（Stopping 等）を全件再構築を待たずにその場で表示できる。
- リコンサイラの契約として「更新コールバックで扱わない表示フィールドはすべてキーに含める」必要がある。
  含め忘れると当該フィールドの表示が陳腐化する（Container のキーに Name/Image/CreatedAt を含めるのは
  このため）。
- Network/Volume は背景での自動再同期や `_pendingOperations` 相当の busy 追跡を持たない。ユーザー起因の
  削除中に別のリフレッシュが走り、当該行のキーが変わって作り直された場合は行の busy 表示が失われ得るが、
  これは従来の Clear+Add（再同期のたびに必ず busy 表示を失っていた）より悪化しておらず、
  名前単位の pending 追跡導入はスコープ外とした。
- 影響を受ける設計ドキュメント: [`docs/design/containers-view.md`](../design/containers-view.md)、
  [`docs/design/images-view.md`](../design/images-view.md)、
  [`docs/design/networks-view.md`](../design/networks-view.md)、
  [`docs/design/volumes-view.md`](../design/volumes-view.md)。
