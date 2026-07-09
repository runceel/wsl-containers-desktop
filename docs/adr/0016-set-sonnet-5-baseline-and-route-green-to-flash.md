# 0016. モデルルーティングのベースラインをsonnet-5 mediumに定めGreenフェーズをFlash(条件付き)にする

## Status

Accepted

## Context

- ADR-0004 / ADR-0008 でモデルルーティングを導入したが、「判断を伴う作業」に使う
  「通常の高性能モデル（既定モデル）」が具体的にどのモデルかは明文化していなかった。
  運用上、既定モデルに常時 opus 級の高コストモデルを使うとコスト効率が悪い。
- 開発フローの大半（詳細設計・テスト作成・レビュー・Green/Refactor・ADR本文）は判断を伴うため
  Flash には落とせないが、これらすべてに opus 級を使うのは過剰である。`claude-sonnet-5`(medium) は
  判断集約型の作業に十分な品質を持ち、opus より安価である。
- ADR-0004 は TDD Green フェーズを一律「対象外（＝Flash非対象）」としていた。しかし ADR-0008 による
  テスト作成フェーズの完了条件強化（具体的な入出力・アサーション値・エッジケースまで確定）を前提とすれば、
  Green は「確定済み仕様のテストを通す最小実装」であり概ね機械的である。ただし Green は初めて
  層配置（Domain/Application/Infrastructure/Presentation）を決める判断を含むため、無条件のFlash化には
  品質低下リスクが残る。
- 検討した代替案:
  - 既定モデルを opus 級のまま据え置く案 → 却下。通常運転のコストが高く、判断系の大半は
    sonnet-5 で品質を維持できる。
  - Green を Flash 非対象のまま据え置く案 → 却下。仕様確定済みの Green は機械的で、
    コスト削減機会を逃す。
  - Green を無条件で Flash にする案 → 却下。新規の層配置・設計判断が必要な場合に品質低下リスクが残る。
  - Refactor も Flash にする案 → 却下。テストは挙動しか守らず、設計品質（命名・責務分離）は
    Flash で劣化しうる。Refactor はベースライン維持とする。
  - 肝（詳細設計・テスト作成）の生産を Flash にし rubber-duck で品質保証する案 → 却下。
    rubber-duck はレビューであって品質を生産しない。抜けたテストは落ちない等、レビューでは
    検出しにくい欠陥が残るため、肝の生産はベースラインを維持する。

## Decision

- モデルルーティングの「既定（ベースライン）モデル」を **`claude-sonnet-5`(medium effort)** と定める。
  判断を伴う作業（機能設計・詳細設計・テスト作成・rubber-duck レビュー・`tdd-refactor`・`adr-writer`・
  ADR本文執筆）はベースラインで行う。より高い品質が必要な場合は、セッションのドライバモデルを
  人間が opus 等へ切り替える（判断系エージェントにはモデルをピン留めしない）。
- TDD Green フェーズについて、`tdd-green` agent の既定モデルを **`mai-code-1-flash-picker`** とする。
  ただし `tdd-red` agent と同型のエスカレーション条項を課す: コンパイル/実装のために
  **新しい非自明な層配置・設計判断**（詳細設計で未確定のもの）が必要になった場合は、その場で
  ベースラインモデルへエスカレーションする。前提（仕様・アサーション値の確定）が満たされない場合は
  Flash で進めず「テスト作成」フェーズへ差し戻す。
- Flash に固定するのは `tdd-red` / `quick-fix` / `tdd-green`（条件付き）の 3 つのみとする。
  その他の判断系エージェントはモデルをピン留めせず、セッションのドライバモデル（ベースライン）を継承する。
- 「肝」（詳細設計・テスト作成・振り返り）は、ベースラインの生産 + `rubber-duck` レビューのペアで
  品質を担保する。
- 本ADRは ADR-0004 / ADR-0008 を置き換えるものではなく、両者を維持したまま、(1)「既定モデル」を
  `claude-sonnet-5`(medium) と具体化し、(2) Green フェーズのルーティングを拡張・修正するものである。

## Consequences

- 通常運転のモデルグレードが opus 級 → `claude-sonnet-5`(medium) に下がり、開発フロー全体の
  トークンコストを削減できる。重い作業時のみ人間が opus を選ぶことで、品質とコストのバランスを取れる。
- 判断系エージェント（`tdd-refactor` / `adr-writer` 等）はモデルをピン留めしないため、人間が
  セッションを opus で回せば肝の生産も自動で opus 品質になる（over-constrain しない）。
- Green のコスト削減は、テスト作成フェーズ（ADR-0008 の完了条件）が確実に守られていることが前提。
  仕様が曖昧なまま Green を Flash で進めると層配置ミス等の品質低下が起きうるため、エスカレーション
  条項の順守が必須。`feature-workflow` skill の DoD をこの前提とセットで運用する。
- 「肝の生産を Flash にしない」ことで、rubber-duck が検出しにくい欠陥（抜けたテスト等）を、
  ベースライン品質の生産側で予防できる。
- Flash に固定するエージェントが 2 つ（`tdd-red` / `quick-fix`）→ 3 つ（＋ `tdd-green`）になる。
- 影響を受けるファイル:
  [`.github/agents/tdd-green.agent.md`](../../.github/agents/tdd-green.agent.md)、
  [`AGENTS.md`](../../AGENTS.md)（モデルルーティング表）、
  [`.github/skills/feature-workflow/SKILL.md`](../../.github/skills/feature-workflow/SKILL.md)、
  [`.github/instructions/tests.instructions.md`](../../.github/instructions/tests.instructions.md)。
- ADR-0004・ADR-0008 は本ADRにより置き換えられず、引き続き有効（`Status: Accepted`）。
