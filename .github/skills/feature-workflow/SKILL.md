---
name: feature-workflow
description: "本リポジトリの必須開発フロー(機能設計→詳細設計[ラバーダック]→テスト作成[ラバーダック]→TDD実装(Red/Green/Refactor)→テスト(+E2E)→振り返り[ラバーダック])をオーケストレーションする。Use this skill whenever the user asks to implement a new feature, start a feature, fix a bug with proper process, or says 新機能を実装, 機能を追加, 設計から始めて, feature実装, ちゃんとした手順で実装して, TDDで実装."
---

# Feature Workflow（機能開発フロー オーケストレーション）

本リポジトリでは、機能追加・仕様のある変更を行う際、必ず以下の6フェーズを順番に実施する
（[`AGENTS.md`](../../../AGENTS.md) の「開発フロー」節の実行手順版）。

単純な機械的修正（タイポ修正等）は対象外。[`quick-fix` agent](../../agents/quick-fix.agent.md)（[ADR-0004](../../../docs/adr/0004-adopt-model-routing-for-simple-changes.md)）を使う。

## フェーズと使うツール

| # | フェーズ | やること | 使うagent/skill |
|---|---|---|---|
| 1 | 機能設計 | 何を作るか、スコープ、ユーザー価値を明確にする | （このagent自身で会話しながら詰める） |
| 2 | 詳細設計 | 技術的な設計判断（層構成上の配置、インターフェース設計等）を行う | `task` agent_type: `rubber-duck` でレビューを受ける |
| 3 | テスト作成 | 詳細設計の仕様をMSTestのテストとして書き下す（まだ失敗させない） | `task` agent_type: `rubber-duck` でテスト内容をレビューする |
| 4 | 実装(TDD) | Red→Green→Refactorを1振る舞いずつ繰り返す | `.github/agents/tdd-red.agent.md` → `tdd-green.agent.md` → `tdd-refactor.agent.md` |
| 5 | テスト | 単体テストに加え、UIに関わる変更は自動E2Eも行う | 既存の `winui-ui-testing` skill（winui plugin） |
| 6 | 振り返り | 設計判断・実装の妥当性をレビューし、必要ならADR/design docを更新 | `task` agent_type: `rubber-duck`、必要なら `adr-writer` agent |

## 各フェーズの完了条件 (Definition of Done)

1. **機能設計**: スコープと非スコープが明文化され、ユーザーの合意が取れている。
2. **詳細設計**: `rubber-duck` の重大な指摘が解消済み。ADR化が必要な決定は洗い出し済み
   （必要なら [`adr-workflow`](../adr-workflow/SKILL.md) を使ってこの時点でADRを書いてよい）。
3. **テスト作成**: `rubber-duck` のレビューで「仕様の抜け漏れ」の指摘が解消済み。
4. **実装(TDD)**: 対象の振る舞いすべてがGreenで、Refactor後もGreenを維持している。
5. **テスト**: 単体テスト・（該当する場合）E2Eテストがすべてパスしている。
6. **振り返り**: `rubber-duck` の指摘を反映済み。ADR・[`docs/design/`](../../../docs/design/README.md) が
   実装と一致するよう更新済み（[`design-doc-maintenance`](../design-doc-maintenance/SKILL.md) を使う）。

## 進め方の原則

- フェーズを飛ばさない。特に「テスト作成」を「実装」より先に行うことを厳守する（[ADR-0002](../../../docs/adr/0002-adopt-strict-tdd-workflow.md)）。
- 1回のTDDサイクルで扱う振る舞いは1つ。複数の振る舞いがある機能は、
  フェーズ2〜4を振る舞いごとに小さく繰り返す。
- ラバーダックは `task` ツールで `agent_type: "rubber-duck"` を使う。それぞれのタイミングで、
  「何をレビューしてほしいか」（設計内容 / テスト内容 / 実装結果）を明示してから依頼する。
- 層構成（Domain/Application/Infrastructure/Presentation）への配置は
  [ADR-0005](../../../docs/adr/0005-adopt-clean-architecture-layering.md) と
  [`csharp.instructions.md`](../../instructions/csharp.instructions.md) に従う。
