# Architecture Decision Records (ADR)

このディレクトリには、本プロジェクトにおける重要な設計判断（アーキテクチャだけでなく、開発プロセスやツール選定などプロジェクトに大きな影響を与える決定を含む）を記録する。

## なぜADRを書くのか

- 「なぜそうしたか」という**経緯・理由**は、コードや設計ドキュメント本体には書かない。
  設計ドキュメント（[`docs/design/`](../design/README.md)）は常に**現在の姿の最新スナップショット**のみを保持し、
  過去の意思決定の理由や却下した代替案はADRに残す。
- 新しいコントリビューター（人間・Copilotエージェント問わず）が「なぜこの構造になっているか」を
  後から追えるようにする。

## ファイル命名

```
docs/adr/NNNN-kebab-case-title.md
```

- `NNNN` は4桁の連番（`0001`, `0002`, ...）。**歯抜けにしない。既存ADRの番号は変更しない。**
- タイトルは決定の内容を表す短い英語スラッグ（例: `0005-adopt-clean-architecture-layering.md`）。

## ステータス

各ADRは以下のいずれかの `Status` を持つ。

| Status | 意味 |
|---|---|
| `Proposed` | 提案中。まだ合意が取れていない。 |
| `Accepted` | 採用済み。現在有効な決定。 |
| `Superseded by ADR-XXXX` | 別のADRによって置き換えられた。 |
| `Deprecated` | 有効ではなくなったが、置き換え先のADRは存在しない。 |

## 不変性ルール（最重要）

- **一度書いたADRの本文（Context / Decision / Consequences）は、内容を変更しない。**
  誤字修正など軽微な修正を除き、後から書き換えない。
- 決定を覆す・変更する場合は、**新しいADRを追加**し、
  - 新ADRの `Context` に「ADR-XXXX を置き換える」旨と理由を書く
  - 古いADRの `Status` を `Superseded by ADR-YYYY` に更新する（本文には手を入れない）
- これにより、ADR全体を通読すると意思決定の変遷がそのまま履歴として読める状態を保つ。

## 新しいADRを書く

1. [`template.md`](template.md) をコピーして `NNNN-title.md` を作成する。
2. `.github/skills/adr-workflow/SKILL.md` の手順、または `.github/agents/adr-writer.agent.md` を使う。
3. 番号は既存の最大値+1を使う。
4. 関連する `docs/design/` のドキュメントがあれば、そのドキュメントからこのADRへリンクを張る
   （逆方向、設計ドキュメント→ADR、が正しいリンクの向き）。

## 現在のADR一覧

| ADR | タイトル | Status |
|---|---|---|
| [0001](0001-record-architecture-decisions.md) | ADR運用を採用する | Accepted |
| [0002](0002-adopt-strict-tdd-workflow.md) | 厳密なTDD(Red-Green-Refactor)を採用する | Accepted |
| [0003](0003-select-mstest-as-unit-test-framework.md) | 単体テストフレームワークとしてMSTestを採用する | Accepted |
| [0004](0004-adopt-model-routing-for-simple-changes.md) | 単純な修正向けのモデルルーティング方針を採用する | Accepted |
| [0005](0005-adopt-clean-architecture-layering.md) | クリーンアーキテクチャに基づく層分割を採用する | Accepted |
| [0006](0006-adopt-slnx-solution-file-format.md) | C#ソリューションファイル形式として.slnxを採用する | Accepted |
| [0007](0007-disable-windows-app-sdk-deployment-manager-auto-initialize.md) | WslContainersDesktop.AppでWindowsAppSdkDeploymentManagerInitializeを無効化する | Accepted |
| [0008](0008-expand-model-routing-to-mechanical-workflow-steps.md) | モデルルーティング方針をワークフロー内の機械的ステップへ拡張する | Accepted |
| [0009](0009-wrap-wslc-cli-for-infrastructure-layer.md) | Infrastructure層で`wslc` CLIのプロセス起動ラップ方式を採用する | Accepted |
| [0010](0010-adopt-di-container-for-presentation.md) | PresentationにDIコンテナ(Microsoft.Extensions.DependencyInjection)を導入する | Accepted |
| [0011](0011-do-not-auto-restart-wsl-on-settings-change.md) | 設定変更時にWSLを自動再起動せず案内のみ行う | Accepted |
| [0012](0012-wsl-environment-detection-and-wslconfig-editing.md) | WSL環境検出と`.wslconfig`編集の方式を採用する | Accepted |
| [0013](0013-adopt-differential-updates-for-list-views.md) | 一覧ViewModelの更新に差分更新(ObservableCollectionReconciler)を採用する | Accepted |
| [0014](0014-dashboard-as-default-landing-and-first-nav-item.md) | ダッシュボード（概要）画面を既定の起動画面かつ先頭のナビゲーション項目にする | Accepted |
| [0015](0015-promote-navigation-viewmodel-to-di-singleton.md) | `NavigationViewModel`をDIシングルトンに昇格しダッシュボードからの遷移に共有する | Accepted |
| [0016](0016-set-sonnet-5-baseline-and-route-green-to-flash.md) | モデルルーティングのベースラインをsonnet-5 mediumに定めGreenを条件付きFlashにする | Accepted |
| [0017](0017-split-containersviewmodel-and-runtime-client-into-focused-components.md) | `ContainersViewModel`とランタイムクライアントを機能単位のコンポーネントへ分割する | Accepted |
| [0018](0018-adopt-resizable-table-layouts-with-fixed-action-rails.md) | 表形式一覧に列幅変更と固定アクションレールを採用する | Accepted |

新しいADRを追加したら、この表にも1行追加すること。
