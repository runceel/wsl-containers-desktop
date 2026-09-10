---
date: 2026-09-10
roles:
  quick-fix: gpt-5.6-luna
  tdd-red: gpt-5.6-luna
  tdd-green: gpt-5.6-terra
  tdd-refactor: gpt-5.6-terra
  adr-writer: gpt-5.6-terra
  rubber-duck: gpt-5.6-sol
---

# 0019. GPTロールルーティングとワークフロー契約を採用する

## Status

Accepted

## Context

- ADR-0002、ADR-0004、ADR-0008、ADR-0016は、厳密なTDD、フェーズ分離、単純作業の
  モデルルーティング、既定モデルを段階的に定めてきたが、ロールごとのモデル、例外、
  エスカレーション、およびTDDの実行可能な契約を一つの規則として扱う必要があるため、
  これら4件を置き換える。
- ユーザーは通常作業にTerra、重要なレビューにSol、軽量作業にLunaを用いるGPT優先の
  運用を承認した。価格・性能の優劣はリポジトリのベンチマークで実証したものではない。
- 2026-09-10に確認したGitHub公式の
  [モデル比較](https://docs.github.com/en/copilot/reference/ai-models/model-comparison)、
  [モデルと価格](https://docs.github.com/en/copilot/reference/copilot-billing/models-and-pricing)、
  [カスタムエージェント設定](https://docs.github.com/en/copilot/reference/custom-agents-configuration)
  を運用上の前提とする。標準USD/100万トークン（入力/出力）はLuna 0.20/1.20、
  Terra 2/12、Sol 4/20、Astra 10/50であるが、キャッシュ利用、reasoning、再試行により
  実コストは変動する。
- 検討した代替案:
  - Sonnet 5とMAI 1.1を継続する案 → ユーザーのGPT優先方針に合わないため不採用。
  - 全作業をAstraにする案 → 日常作業のコストが過大なため不採用。
  - 全作業をLunaにする案 → 設計・レビューの判断品質リスクがあるため不採用。
  - セッションのモデル継承に委ねる案 → ロール別コストが非決定的になるため不採用。

## Decision

- 機能追加・仕様のある変更には、(1)機能設計、(2)詳細設計、(3)テスト設計、
  (4)実行可能なRed→Green→Refactor、(5)全テストスイート（UI変更時はUI E2Eを含む）、
  (6)振り返りの6フェーズを順に適用し、GPTロールと以下の契約で厳密なTDDおよび
  フェーズ分離を維持する。
- ロールの既定モデルは、`quick-fix`と`TDD Red`を`gpt-5.6-luna`、`TDD Green`、
  `TDD Refactor`、`adr-writer`を`gpt-5.6-terra`、読み取り専用の新しい
  `rubber-duck`を`gpt-5.6-sol`とする。オーケストレーションセッションにはTerraを
  推奨するが、文書の編集によって実行中セッションのモデルを切り替えることはできない。
- task実行では、対応するモデルで`model`と`reasoning_effort`を明示する。Luna/Terraは
  `medium`、Sol/Astraは`high`、`context_tier`は特段の必要がない限り`default`とする。
  利用不能なGPTモデルでは停止してGPTの代替を質問し、非GPTへ自動フォールバックしない。
- 優先順位は、ユーザーのタスク上書き、必須エスカレーション、承認済みの単純Greenに
  限るLuna最適化、ロール既定の順とする。ユーザーのモデル制約がエスカレーションを
  妨げる場合は質問し、黙って下位モデルへ落とさない。
- フェーズ3は、レビュー済みの具体的な入力、出力、アサーション、エッジケースを備えた
  テスト設計を成果物とする。実行可能なテストはRedでのみ作成する。Redは振る舞いの実装を
  禁止し、テスト記述後にコンパイルのため必要な、レビュー済み最小シグネチャと決定的な
  プレースホルダー戻り値だけを例外的に許可する。`void`は空、非`void`は定数または
  `default`、非同期は完了済み結果とし、値はレビュー済みアサーションを失敗させるものを
  テスト設計で指定する。分岐、I/O、ドメインロジック、例外を投げるスタブは禁止する。
  Redはコンパイル・環境・`NotImplementedException`ではなく、意図したアサーションで失敗
  しなければならない。
- 各R/G/Rサイクルは1振る舞いだけを扱う。Greenはテストを編集せず、Refactorは振る舞いも
  アサーションも変えない。各サイクルでは影響を受けるテストを実行し、フェーズ5では
  全スイートを実行する。リリースでは変更前後に全スイートを実行する。失敗を黙ってスキップしない。
- LunaでGreenを行えるのは、レビュー済み固定契約、既存の機械的パターン、新しい設計・
  外部調整・並行性・状態遷移がない場合だけであり、Greenフェーズ全体への包括適用ではない。
  仕様不足は設計へ戻し、同じ失敗が修正2回後も続けば停止して再評価し、
  Luna→Terra→Solへエスカレーションする。Astraはユーザー確認がある場合だけ用いる。
  高位実装には同じフェーズ専用エージェントをモデル上書きして使い、レビュー担当を
  実装担当にしない。
- 詳細設計、テスト設計、振り返りは独立した読み取り専用rubber-duckレビューを行う。
  文書が必要な正式仕様のレビューはフェーズ1に残す。reviewer agentは読み取り・検索・
  Web確認だけを行い、編集、shell、taskを行わない。登録されていない場合のみ、明示的な
  読み取り専用指示付きの`general-purpose` Solを代替にできるが、これは同等のツール
  サンドボックスではない。再帰的な委譲・レビュー循環は禁止する。
- 機械的変更は親が直接2ツール呼び出し以内で完了できる場合にエージェントを強制しない。
  より大きい独立バッチは`quick-fix`を用いる。ただし、nullガード、新規プロパティ、
  新しい振る舞いを伴うボイラープレートは機械的変更から除外する。

## Consequences

- ユーザー承認済みのGPTロール、推論強度、TDD境界、失敗時の判断者が明文化され、
  コストと品質のトレードオフを追跡可能にする。一方、実費節約額は推定しない。
- フェーズ3の精度とRedの失敗確認が必須となり、作業開始は遅くなり得るが、Greenで仕様を
  作り替えることを防ぐ。モデル利用不能、仕様不足、反復失敗では自動続行せず停止・質問・
  再評価が必要になる。
- 運用の現在のスナップショットは新しいアプリ設計文書ではなく
  [`AGENTS.md`](../../AGENTS.md)である。本ADRは
  [ADR-0002](0002-adopt-strict-tdd-workflow.md)、
  [ADR-0004](0004-adopt-model-routing-for-simple-changes.md)、
  [ADR-0008](0008-expand-model-routing-to-mechanical-workflow-steps.md)、
  [ADR-0016](0016-set-sonnet-5-baseline-and-route-green-to-flash.md)を完全に置き換える。
