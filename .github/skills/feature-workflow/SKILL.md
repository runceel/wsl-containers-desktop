---
name: feature-workflow
description: "本リポジトリの必須開発フロー(機能設計→詳細設計[レビュー]→テスト設計[レビュー]→TDD実装(Red/Green/Refactor)→全体テスト(+E2E)→振り返り[レビュー])をGPTの役割別モデルで進める。Use this skill whenever the user asks to implement a feature or fix a behavior-changing bug, or says 新機能を実装, 機能を追加, バグ修正, 設計から始めて, TDDで実装. Do not use for mechanical edits, skills/agents maintenance, or documentation-only changes."
---

# Feature Workflow（機能開発フロー オーケストレーション）

本リポジトリでは、機能追加・仕様のある変更を行う際、必ず以下の6フェーズを順番に実施する
（[`AGENTS.md`](../../../AGENTS.md) の「開発フロー」節の実行手順版）。

単純な機械的修正、skills/agentsの保守、文書のみの更新は対象外。
機械的な小修正は親が直接行い、大きい独立バッチだけ[`quick-fix`](../../agents/quick-fix.agent.md)を使う。
モデルとTDDの方針は[ADR-0019](../../../docs/adr/0019-adopt-gpt-role-routing-and-workflow-contracts.md)を参照。

## 前提条件

- MSTestの既存実行手段と、UI変更時の`winui-ui-testing`が利用可能であること。
- `AGENTS.md`の役割別モデルと、利用可能なagent一覧を確認すること。
  ファイル名と表示名は異なるため、`task`には一覧に公開された正確な`agent_type`を渡す。
- モデル・推論強度・優先順位・エスカレーションは`AGENTS.md`のモデルルーティングを正とする。
  skillを読み込んでも親セッションのモデルは切り替わらない。

## フェーズと使うツール

| # | フェーズ | やること | 使うagent/skill |
|---|---|---|---|
| 1 | 機能設計 | 何を作るか、スコープ、ユーザー価値を明確にする | 親（Terra推奨）。正式な仕様書を作る場合はSolでレビュー |
| 2 | 詳細設計 | 層配置・契約・インターフェースを決める | 親が設計、`rubber-duck`（Sol）がレビュー |
| 3 | テスト設計 | 入力・期待値・アサーション・エッジケースを確定。実行可能なテストはまだ書かない | 親が設計、`rubber-duck`（Sol）がレビュー |
| 4 | 実装(TDD) | Red→Green→Refactorを1振る舞いずつ繰り返す | `tdd-red`（Luna）→ `tdd-green`（Terra、条件付きLuna）→ `tdd-refactor`（Terra） |
| 5 | テスト | 全スイートと、UI変更時のE2E | 既存のテストskill/agent、`winui-ui-testing` |
| 6 | 振り返り | 設計判断・実装の妥当性をレビューし、必要ならADR/design docを更新 | `rubber-duck`（Sol）、本文作成は`adr-writer`（Terra） |

## 各フェーズの完了条件 (Definition of Done)

1. **機能設計**: スコープと非スコープが明文化され、ユーザーの合意が取れている。
2. **詳細設計**: `rubber-duck` の重大な指摘が解消済み。ADR化が必要な決定は洗い出し済み
   （必要なら [`adr-workflow`](../adr-workflow/SKILL.md) を使ってこの時点でADRを書いてよい）。
3. **テスト設計**: 具体的な入出力・アサーション値・エッジケースが確定し、レビューの重大な指摘が解消済み。
   Redでコンパイル用スタブが必要な場合は、最小シグネチャと固定戻り値も事前に決め、
   その値で意図したアサーションが失敗することを設計上確認する。
4. **実装(TDD)**: 対象の振る舞いすべてがGreenで、Refactor後もGreenを維持している。
5. **テスト**: 単体テスト・（該当する場合）E2Eテストがすべてパスしている。
6. **振り返り**: `rubber-duck` の指摘を反映済み。ADR・[`docs/design/`](../../../docs/design/README.md) が
   実装と一致するよう更新済み（[`design-doc-maintenance`](../design-doc-maintenance/SKILL.md) を使う）。

## 進め方の原則

- フェーズを飛ばさない。テスト設計の後、Redでテストを書き、意図したアサーション失敗を確認してから
  Greenで振る舞いを実装する。コンパイル・環境エラー・例外を投げるスタブはRedの証拠にならない。
- 1回のTDDサイクルで扱う振る舞いは1つ。複数の振る舞いがある機能は、
  フェーズ2〜4を振る舞いごとに小さく繰り返す。
- ラバーダックの登録を確認して呼ぶ。未登録時の`general-purpose` Solによる読み取り専用代替は
  `AGENTS.md`の「独立レビュー」に従う。レビューを省略したり、存在しないagent識別子を呼んだりしない。
- 各agentには対象ファイル、確定仕様、直前の実行結果、変更可能範囲、完了条件を渡す。
  結果には変更ファイル、commandと結果、未実行範囲、未解決事項を要求する。次フェーズの起動は親が行う。
- 各サイクルでは対象と影響範囲のテストをまとめて実行し、全スイートはフェーズ5で実行する。
  並行できる独立調査だけ並列化し、同じファイルの編集や依存するRed/Greenを並列に進めない。
- 層構成（Domain/Application/Infrastructure/Presentation）への配置は
  [ADR-0005](../../../docs/adr/0005-adopt-clean-architecture-layering.md) と
  [`csharp.instructions.md`](../../instructions/csharp.instructions.md) に従う。

## 停止・差し戻し

| 状況 | 対応 |
|---|---|
| 仕様・期待値・層配置が未確定 | 詳細設計またはテスト設計へ戻す。モデルを上げるだけで解決したことにしない |
| 同じ失敗への修正が2回失敗 | 親へ証拠を返し、AGENTS.mdの順でモデルを上げて再評価 |
| モデル・専用TDD agent・レビュー代替が利用不能 | 未完了として停止し、モデル代替または環境整備を確認 |
| テスト・E2Eを実行できない | 実行不能の理由と未確認範囲を明示。フェーズ完了にしない |

## 振り返りと出力

成果物、仕様の充足状況、未解決事項を簡潔に返す。運用とskillの再現可能なずれがあれば、
親が修正の要否を判断する。一時的な障害や特定タスクの値を共通ルールに固定しない。
