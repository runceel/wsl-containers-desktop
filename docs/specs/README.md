# 機能仕様書 (`docs/specs/`)

このディレクトリには、本プロジェクトで実装する**個別機能の仕様書**を置く。
[`AGENTS.md`](../../AGENTS.md) の開発フロー フェーズ1「機能設計」の成果物に相当し、
「何を作るか・なぜ作るか・スコープはどこまでか」を明文化したものである。
**設計の詳細（層構成上の配置、クラス設計、UIレイアウト等）はここには書かない**
（それらはフェーズ2「詳細設計」の成果物として [`docs/design/`](../design/README.md) に記録する）。

## ADR / design / reference との違い

| ディレクトリ | 内容 | 更新方針 |
|---|---|---|
| `docs/adr/` | 自分たちの意思決定 | 本文は不変。覆す場合は新規ADR追加 |
| `docs/design/` | 自分たちの設計の最新スナップショット | 上書き更新（経緯は書かない） |
| `docs/reference/` | 外部プラットフォーム・製品の仕様・現状 | 外部の変化に合わせて上書き更新 |
| `docs/specs/`（本ディレクトリ） | 個別機能の仕様（何を作るか。実装前の提案） | 実装完了後も**削除せず残す**（機能カタログとして運用）。仕様に変更があれば上書き更新 |

## 運用ルール

- 1機能につき1ファイル。ファイル名は `NNNN-kebab-case-title.md`（4桁連番、実装予定順）。
- 各仕様書は以下の構成で書く。技術的な実装方針・設計判断は含めない。
  1. 概要
  2. ユーザー価値（なぜ必要か）
  3. スコープ（含むもの）
  4. 非スコープ（含まないもの・将来検討）
  5. 前提条件（依存する前段の仕様書）
  6. 受け入れ基準（機能として何が確認できればよいか。実装方法は問わない）
  7. 関連ドキュメント
  8. 関連Issue
- 仕様書は起票時に `task` agent_type: `rubber-duck` でレビューを受け、指摘を反映してから確定する。
- 仕様書ごとに対応するGitHub issueを1件作成し、「関連Issue」セクションにリンクする。
- 詳細設計フェーズに進んだら、確定した設計内容は `docs/design/` 側に記録する
  （本仕様書はスコープ・受け入れ基準の記録として残り続ける）。

## 実装順序と一覧

段階的リリース方針（土台 → 機能セットを順番に追加）に基づく実装予定順。

| # | 機能 | 仕様書 | Issue |
|---|---|---|---|
| 0 | 土台（空のWinUIウィンドウ起動） | [0000-foundation-empty-window.md](0000-foundation-empty-window.md) | [#3](https://github.com/runceel/wsl-containers-desktop/issues/3) |
| 1 | コンテナ一覧・基本操作 | [0001-container-list-basic-operations.md](0001-container-list-basic-operations.md) | [#4](https://github.com/runceel/wsl-containers-desktop/issues/4) |
| 2 | コンテナログ表示 | [0002-container-log-viewer.md](0002-container-log-viewer.md) | [#10](https://github.com/runceel/wsl-containers-desktop/issues/10) |
| 3 | イメージ管理 | [0003-image-management.md](0003-image-management.md) | [#6](https://github.com/runceel/wsl-containers-desktop/issues/6) |
| 4 | コンテナ詳細・exec | [0004-container-detail-exec.md](0004-container-detail-exec.md) | [#11](https://github.com/runceel/wsl-containers-desktop/issues/11) |
| 5 | ボリューム管理 | [0005-volume-management.md](0005-volume-management.md) | [#9](https://github.com/runceel/wsl-containers-desktop/issues/9) |
| 6 | ネットワーク管理 | [0006-network-management.md](0006-network-management.md) | [#8](https://github.com/runceel/wsl-containers-desktop/issues/8) |
| 7 | 設定画面 | [0007-settings.md](0007-settings.md) | [#7](https://github.com/runceel/wsl-containers-desktop/issues/7) |
| 8 | ダッシュボード/概要画面 | [0008-dashboard-overview.md](0008-dashboard-overview.md) | [#5](https://github.com/runceel/wsl-containers-desktop/issues/5) |

エンタープライズ向け機能（GPOポリシー等）・GPU管理UIは、個人向けアプリの初期スコープからは
意図的に外している（[`docs/reference/wsl-containers-platform.md`](../reference/wsl-containers-platform.md) 参照）。
