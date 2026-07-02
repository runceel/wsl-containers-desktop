# 0001. Architecture Decision Recordを採用する

## Status

Accepted

## Context

- 本リポジトリ（WSL Containers Desktop）は開発初期段階であり、これから多くの技術的判断
  （プロジェクト構成、テスト戦略、ツール選定など）を行っていく。
- Copilotエージェントを含む複数の開発者が関わるため、「なぜその構造・選択になっているか」を
  後から追跡できる仕組みが必要。
- 一方で、設計ドキュメント（`docs/design/`）は常に最新の状態を分かりやすく示す
  スナップショットとして保ちたく、過去の経緯や却下案で埋めたくない。

## Decision

- 本プロジェクトに影響を与える重要な決定（アーキテクチャ、開発プロセス、主要ツール選定など）は
  `docs/adr/` 配下にADR (Architecture Decision Record) として記録する。
- ADRの運用ルール（命名規則・ステータス遷移・不変性ルール）は [`docs/adr/README.md`](README.md) に従う。
- 設計ドキュメントは経緯を書かず、必要な場合は該当ADRへリンクする。

## Consequences

- 意思決定の理由が後から追跡可能になり、設計ドキュメントを常に簡潔な最新スナップショットに保てる。
- ADRを書く分だけ、重要な決定をする際の作業がわずかに増える。
  → `.github/agents/adr-writer.agent.md` と `.github/skills/adr-workflow/SKILL.md` で
    作成コストを下げる。
- 以降のADR（0002〜）は、このADR運用ルールに従って記載される。
