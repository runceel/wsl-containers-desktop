---
name: design-doc-maintenance
description: "docs/design配下の設計ドキュメントを、経緯を含めない最新スナップショットとして更新する手順。Use this skill whenever the user asks to update design docs, keep documentation current, or says 設計ドキュメントを更新, design docを直して, アーキテクチャドキュメントを最新化して."
---

# Design Doc Maintenance

設計ドキュメントの運用ルール本体は [`docs/design/README.md`](../../../docs/design/README.md) を参照。
このskillは「実装が固まった/変わったので設計ドキュメントを更新する」実務手順をまとめたもの。

## 原則（再掲）

- `docs/design/` は**現在の姿だけ**を書く。過去の経緯・検討過程・却下案は書かない。
- 「なぜこうなっているか」を書きたくなったら、理由ではなく該当ADRへのリンクを1行添えるだけにする。
- 追記して履歴を残すのではなく、内容を**上書き**する。
- **注意**: 外部プラットフォーム（例: WSL Containers本体の仕様）についての情報は
  `docs/design/` ではなく [`docs/reference/`](../../../docs/reference/README.md) に置く
  （こちらは自分たちの設計、`docs/reference/` は外部の事実、という区別）。

## 更新するタイミング

- `feature-workflow` skillの「詳細設計」フェーズで設計が固まった時点。
- `tdd-refactor` agentのフェーズで、当初の設計から実装が変わった時点。
- 新しいADRを起票し、それが設計ドキュメントの内容に影響する場合。

## 手順

1. 変更が影響するドキュメントを特定する（`architecture-overview.md`、または個別機能の
   `docs/design/<feature-name>.md`）。
2. 現在の実装・設計と一致するよう、該当箇所を**書き換える**（差分として経緯を残さない）。
3. 採用理由の説明が必要な箇所には、理由の文章ではなく `詳細は ADR-000X を参照` のようなリンクのみを置く。
4. 新しい機能領域を初めてドキュメント化する場合は、`docs/design/<feature-name>.md` を新規作成し、
   [`docs/design/README.md`](../../../docs/design/README.md) の「ドキュメント構成」表に追記する。
5. 図が必要な場合はMermaidを使う（`architecture-overview.md` の記法を参考にする）。

## やってはいけないこと

- 「以前は〜だったが、今は〜」のような経緯の記述を残すこと。
- ADRに書くべき理由・トレードオフの説明を設計ドキュメント本体に書き写すこと。
