---
name: adr-workflow
description: "設計判断・プロセス決定をADR (Architecture Decision Record) として作成・更新する具体的な手順。Use this skill whenever the user asks to record a decision, write an ADR, or says ADRを書きたい, 設計判断を記録したい, ADR作って, この決定を残したい."
---

# ADR Workflow

ADRの運用ルールの全体像は [`docs/adr/README.md`](../../../docs/adr/README.md) を参照。
このskillは「今まさにADRを1本書く/更新する」ときの実務手順をまとめたもの。

## いつADRを書くべきか

以下のいずれかに該当する決定は、口頭やコミットメッセージだけで済ませず、ADRとして残す。

- アーキテクチャ・プロジェクト構成に関わる決定（例: 層構成、依存方向、主要ライブラリ選定）
- 開発プロセス・ツールに関わる決定（例: テストフレームワーク、モデルルーティング方針）
- 一度決めたら覆すコストが高い決定（データ永続化方式、外部連携方式など）

逆に、些細な実装詳細（変数名、内部のprivateメソッド分割など）はADR化しない。

## 手順

1. `.github/agents/adr-writer.agent.md` を使うか、以下を手動で行う。
2. `docs/adr/` の既存ファイル名から次の連番を決める（歯抜けにしない）。
3. [`docs/adr/template.md`](../../../docs/adr/template.md) をコピーして新規ファイルを作成。
4. `Context` / `Decision` / `Consequences` を埋める（[`docs/adr/README.md`](../../../docs/adr/README.md) の
   不変性ルールを厳守: 一度書いたら本文は書き換えない）。
5. [`docs/adr/README.md`](../../../docs/adr/README.md) の一覧表に1行追加する。
6. 関連する `docs/design/` のドキュメントがあれば、そこから今回のADRへリンクを追加する。

## 既存の決定を覆す場合

- 古いADRの本文は変更しない。`Status` だけを `Superseded by ADR-YYYY` に変更する。
- 新しいADRの `Context` に「なぜADR-XXXXを置き換えるのか」を書く。
- `docs/adr/README.md` の一覧表のStatus列も更新する。

## rubber-duckとの関係

決定内容自体に自信がない場合は、ADRを書く前に `task` ツールで `agent_type: "rubber-duck"` を使い、
決定の妥当性をレビューしてから起票する（[`feature-workflow`](../feature-workflow/SKILL.md) の
詳細設計フェーズと同じ扱い）。
