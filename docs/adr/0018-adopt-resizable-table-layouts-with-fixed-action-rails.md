# 0018. 表形式一覧に列幅変更と固定アクションレールを採用する

## Status

Accepted

## Context

- [Issue #62 共通GridViewの操作性と機能制限を改善](https://github.com/runceel/wsl-containers-desktop/issues/62)
  では、Containers、Images、Volumes、Networks、Dashboard stats の表形式一覧について、列幅変更、
  画面遷移後の幅の維持、狭い画面でも主要操作へアクセスできる共通仕様が求められている。これらの一覧は
  いずれも WinUI の `ListView` と `Grid` を組み合わせた表形式レイアウトで実装されている。
- ヘッダーと各行は別々の `Grid` であり、行は `DataTemplate` から仮想化・再実体化されるため、
  ユーザーが変更した列幅をヘッダー、実体化済みの行、後から実体化される行の間で同期する仕組みが必要になる。
- [`docs/design/containers-view.md`](../design/containers-view.md) には、`DataGrid` がスクロールバー状態更新
  タイマー内で UI スレッド COM 例外を発生させることが記録されている。
- リポジトリ外の互換性スパイクでは、アプリと同じ `net10.0-windows10.0.26100.0`、ARM64、
  Microsoft.WindowsAppSDK 2.2.0 の構成で `CommunityToolkit.WinUI.Controls.Sizers` 8.2.251219 を使用し、
  Windows App SDK をダウングレードせずにビルドと起動に成功した。このスパイクでは、star 幅の変更を
  `ColumnDefinition.Width` のプロパティコールバックで観測して行幅へ同期できること、矢印キーによる
  8 px 単位のサイズ変更、`MinWidth` / `MaxWidth` の順守、キーボードフォーカス、および
  UI Automation 名を確認した。
- 検討した代替案:
  - **`DataGrid` へ置き換える**: リポジトリで既に記録されている UI スレッド COM 例外を再導入するため
    採用しない。
  - **`ColumnDefinition` を直接データバインドする**: ヘッダーと、仮想化・再実体化される
    `DataTemplate` 内の行との同期を十分に信頼できないため採用しない。
  - **ページローカルに列幅を保持する**: ページの再生成時に幅を失うため採用しない。
    設定ファイル等への永続化も、要求されるプロセス寿命を超えるため採用しない。
  - **`Thumb` を使った独自スプリッターを実装する**: Toolkit の互換性ゲートが通過し、Toolkit が
    ポインター、キーボード、カーソル、幅制約、フォーカス表示、Automation Peer の各挙動を既に
    提供するため、フォールバック候補には残すが採用しない。

## Decision

Issue #62 の対象となる5つの一覧では、`DataGrid` へ置き換えずに既存の `ListView` + `Grid` による
表形式実装を維持し、一覧ごとにプロセス寿命の列レイアウトを共有するサイズ変更可能なメタデータ領域と、
数値指定の固定幅アクションレールを組み合わせる。

- `CommunityToolkit.WinUI.Controls.Sizers` 8.2.251219 を追加し、その具象コントロールをページ XAML に
  直接公開せず、薄いプロジェクトコントロール `TableColumnSplitter` を介してのみ使用する。
- `Application.Resources` に5つの独立した `TableColumnLayout` 依存関係オブジェクトを置く。各オブジェクトは
  対応する一覧の列幅をプロセス内だけで保持し、ページ遷移・再生成後も共有され、新しいプロセスでは既定値に
  戻る。設定やファイルには永続化しない。
- Presentation 層の添付ビヘイビアー `TableLayoutBehavior` が、共有 `TableColumnLayout` とヘッダーおよび
  実体化・再実体化された行を同期する。初期方向は Layout → ヘッダー/行、ユーザー操作後は
  ヘッダー → Layout → 行とし、`ColumnDefinition` のデータバインドは使用しない。アンロード時に購読を
  解除し、再入ループを防止する。
- 各一覧のヘッダーと行を、サイズ変更可能なメタデータ領域と、その右側の数値指定の固定幅アクションレールに
  分け、ヘッダーと行で同じレール幅を共有する。メタデータはアクションレールへ重ならないよう明示的に
  クリップし、一覧全体を横スクロールさせない。
- Containers の固定レールでは、頻繁に使う Start / Stop を同じ位置にインライン表示し、状態と保留中操作に
  応じて Play / Stop アイコンを切り替える。各アイコンにはローカライズされたツールチップとアクセシブル名を
  設定し、その他のコマンドは More actions 配下に維持する。
- 各 `TableColumnSplitter` をキーボードフォーカス可能にし、`AutomationId` とローカライズされた
  アクセシブル名を設定する。ポインター操作には `GridSplitter` の文書化された manipulation 経路を使用して
  アプリの E2E テストで検証し、ページテストは Toolkit の具象型ではなく中立な
  `TableColumnSplitter` 契約を検証する。

## Consequences

- 5つの一覧で列幅変更と右端操作の規則が統一され、ページを移動・再生成しても同じプロセス内では一覧ごとの
  列幅が維持される。
- 固定アクションレールとメタデータのクリップにより、メタデータ列を広げても主要操作へアクセスでき、
  一覧全体の横スクロールやアクション領域への重なりを避けられる。
- プロジェクト所有の `TableColumnSplitter` 契約により、ページ XAML とページテストが Toolkit の具象型へ
  直接依存しない。一方で、新しい NuGet 依存関係と薄いラッパーの保守が必要になる。
- `TableLayoutBehavior` には、仮想化された行の実体化、アンロード、プロパティ変更、再入防止を扱う
  独自の同期ライフサイクルコードと、そのテストが必要になる。
- 数値指定の固定幅アクションレールは常に横幅を消費する。残り幅を超えたメタデータはクリップされるため、
  内容を確認するにはユーザーが列を縮めるかウィンドウを広げる必要がある。
- 列幅は新しいプロセスで既定値に戻り、ユーザー設定としては永続化されない。
- 影響を受ける設計ドキュメント:
  [`docs/design/containers-view.md`](../design/containers-view.md)、
  [`docs/design/images-view.md`](../design/images-view.md)、
  [`docs/design/volumes-view.md`](../design/volumes-view.md)、
  [`docs/design/networks-view.md`](../design/networks-view.md)、
  [`docs/design/dashboard-view.md`](../design/dashboard-view.md)。これらは実装後に最新スナップショットへ更新する。
