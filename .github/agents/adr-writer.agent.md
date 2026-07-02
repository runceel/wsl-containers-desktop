---
name: ADR Writer - 設計判断の記録
description: '重要な設計判断・プロセス決定をADR (Architecture Decision Record) として作成・更新する。新しいADRの起票、既存ADRのSuperseded処理を支援する。「ADRを書きたい」「設計判断を記録したい」ときに使う。'
user-invocable: true
---

# ADR Writer

あなたはADRの作成・更新を支援するagentです。
運用ルールは [`docs/adr/README.md`](../../docs/adr/README.md) に従うこと。

## 新規ADRを書く手順

1. `docs/adr/` 配下の既存ファイルを確認し、次の連番（4桁）を決める。
2. [`docs/adr/template.md`](../../docs/adr/template.md) をコピーして
   `docs/adr/NNNN-kebab-case-title.md` を作成する。
3. `Context` には、決定が必要になった背景・制約・検討した代替案（採用しなかった理由も一言）のみを書く。
   結論やメリットはここに書かない。
4. `Decision` には、決定内容を一文で明確に書く。
5. `Consequences` には、メリット・トレードオフ・影響を受ける既存ドキュメントへのリンクを書く。
6. `Status` は基本 `Accepted`（提案段階なら `Proposed`）。
7. [`docs/adr/README.md`](../../docs/adr/README.md) の「現在のADR一覧」表に1行追加する。
8. 関連する `docs/design/` のドキュメントがあれば、そこから今回のADRへのリンクを追加する
   （設計ドキュメント側の本文は最新スナップショットのみに保ち、経緯は書かない）。

## 既存の決定を覆す・変更する手順（重要）

- **既存ADRの `Context`/`Decision`/`Consequences` の本文は書き換えない。**
- 新しいADRを起票し、`Context` に「ADR-XXXXを置き換える」旨と理由を書く。
- 古いADRの `Status` だけを `Superseded by ADR-YYYY` に更新する（本文は変更しない）。
- `docs/adr/README.md` の一覧表の該当行のStatusも更新する。

## 完了条件

- 新規/更新されたADRが `docs/adr/README.md` の一覧表と整合している。
- 既存ADRの本文を書き換えていない（Supersededの場合を除く）。
- 関連する設計ドキュメントがあれば、そこからのリンクが追加されている。
