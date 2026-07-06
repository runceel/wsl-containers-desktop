# 0014. ダッシュボード（概要）画面を既定の起動画面かつ先頭のナビゲーション項目にする

## Status

Accepted

## Context

- [Issue #5 [0008] ダッシュボード・概要画面](https://github.com/runceel/wsl-containers-desktop/issues/5)
  で、各リソース（コンテナ・イメージ・ボリューム・ネットワーク）のサマリと稼働中コンテナの
  リソース使用量を一目で把握できる概要画面が求められた。
- 本機能追加より前は、`NavigationPageKey` の先頭要素は `Containers` であり、`NavigationViewModel` の
  既定の初期ページ（`initialPageKey`）も `Containers` だった。すなわちアプリ起動直後は
  コンテナ一覧が表示されていた。
- 概要画面を追加するにあたり、その配置を次のどれにするか判断が必要になった。
  - **`NavigationView` の先頭 + アプリ起動時の既定表示にする**（採用案）。
  - **既存項目の途中/末尾に置き、起動時の既定は従来どおり Containers のままにする**（却下）。
    概要画面は「まず全体を俯瞰し、そこから各一覧へ入る」という導線の起点であり、起動時に
    表示されないと価値が半減する。Docker Desktop 等の同種ツールも起動時に概要を出す慣習がある。
  - **専用のホーム/ランディングを独立させ、ナビゲーション項目には含めない**（却下）。
    型安全キー（`NavigationPageKey`）による統一的なページ切り替え基盤
    （[`docs/design/presentation-navigation.md`](../design/presentation-navigation.md)）から外れ、
    導線と実装が二重化する。

## Decision

ダッシュボード（概要）画面を、`NavigationView` の**先頭のナビゲーション項目**とし、かつ
**アプリ起動時に最初に表示される既定ページ**とする。

- `NavigationPageKey` の先頭に `Dashboard` を追加する（enum の先頭要素）。
- `NavigationViewModel` の既定の初期ページ（`initialPageKey`）を `NavigationPageKey.Dashboard` にする。
- `MainWindow` のナビゲーション項目も `Dashboard` を先頭に配置する。
- 概要画面は既存のページ切り替え基盤（型安全キー + `NavigationPageRegistry`）にそのまま乗せ、
  独自のランディング機構は作らない。

この方針は Issue #5 以降のトップレベル画面構成に適用する。

## Consequences

- アプリ起動直後にシステム全体の状況（各リソース件数・稼働中コンテナのリソース使用量）が
  俯瞰でき、そこから各一覧画面へ入る自然な導線になる。
- ダッシュボードは概要提示に加え、稼働中コンテナ行から対象コンテナの詳細/ログ表示へ遷移する
  導線を持つ。この遷移の実現方式（Containers 画面のViewModelとの連携）は
  [ADR-0015](0015-promote-navigation-viewmodel-to-di-singleton.md) を参照。
- 既定表示が Containers から Dashboard に変わるため、起動直後の初回描画で複数リソースの
  取得が走る。取得は各リソースごとに独立し、一部が失敗しても他は表示する（部分失敗の許容）。
  詳細は [`docs/design/dashboard-view.md`](../design/dashboard-view.md) を参照。
- 影響を受ける設計ドキュメント: [`docs/design/presentation-navigation.md`](../design/presentation-navigation.md)、
  [`docs/design/dashboard-view.md`](../design/dashboard-view.md)、
  [`docs/design/architecture-overview.md`](../design/architecture-overview.md)。
