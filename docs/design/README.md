# 設計ドキュメント運用ルール

`docs/design/` 配下のドキュメントは、**現時点のシステムの姿を示す最新スナップショット**である。

## 原則

- **過去の経緯・検討の変遷・却下した代替案は書かない。** それらは [`docs/adr/`](../adr/README.md) に記録する。
- ドキュメントは常に「今この瞬間の設計はどうなっているか」だけを説明する。
  実装が変わったら、そのつどこのドキュメントを**上書き更新**する（追記して履歴を残すのではない）。
- 「なぜこの設計になったか」を説明したくなったら、本文には理由を書かず、
  該当するADRへのリンク（例: `詳細は ADR-0005 を参照`）を1行添えるだけにする。
- Gitの差分・PR履歴が経緯の記録であり、ドキュメント自体に経緯を持たせる必要はない。

## ドキュメント構成

| ドキュメント | 内容 |
|---|---|
| [`architecture-overview.md`](architecture-overview.md) | システム全体のレイヤー構成・依存関係の現在のスナップショット |
| [`design-policy.md`](design-policy.md) | WinUI / Fluent Design に基づくUI・ビジュアルデザイン方針の現在のスナップショット |
| [`presentation-navigation.md`](presentation-navigation.md) | Presentation層のナビゲーション基盤・ローカライズ基盤・DI構成の現在のスナップショット |
| [`dashboard-view.md`](dashboard-view.md) | ダッシュボード（概要）画面ViewModel（`DashboardViewModel`）のサマリ件数・稼働中コンテナのリソース使用量表示・遷移導線の現在のスナップショット |
| [`containers-view.md`](containers-view.md) | コンテナ一覧ViewModel（`ContainersViewModel`）の行操作・ログ表示状態管理の現在のスナップショット |
| [`images-view.md`](images-view.md) | イメージ一覧ViewModel（`ImagesViewModel`）のpull・削除・状態管理の現在のスナップショット |
| [`volumes-view.md`](volumes-view.md) | ボリューム一覧ViewModel（`VolumesViewModel`）の作成・削除・使用状況表示の現在のスナップショット |
| [`networks-view.md`](networks-view.md) | ネットワーク一覧ViewModel（`NetworksViewModel`）の作成・削除・使用状況表示の現在のスナップショット |
| [`settings-view.md`](settings-view.md) | 設定ViewModel（`SettingsViewModel`）とWSL連携状態確認・`.wslconfig`リソース制限永続化の現在のスナップショット |
| (機能追加に応じて追加) | 個別機能の設計ドキュメントは `docs/design/<feature-name>.md` として追加していく |

## 更新のタイミング

- 詳細設計フェーズ（[`AGENTS.md`](../../AGENTS.md) の開発フロー参照）で設計内容が固まった時点で、
  該当ドキュメントを更新する。
- 実装フェーズ（TDD Refactorフェーズ）で当初の設計から変更が生じた場合も、
  実装確定時点でドキュメントを追従させる。
- 更新手順の詳細は [`.github/skills/design-doc-maintenance/SKILL.md`](../../.github/skills/design-doc-maintenance/SKILL.md) を参照。
