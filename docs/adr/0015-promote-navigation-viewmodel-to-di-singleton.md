# 0015. `NavigationViewModel` を DI シングルトンに昇格し、ダッシュボードからの遷移に共有する

## Status

Accepted

## Context

- [Issue #5 [0008] ダッシュボード・概要画面](https://github.com/runceel/wsl-containers-desktop/issues/5)
  のダッシュボードは、稼働中コンテナの各行から「詳細」「ログ」を開く導線を持つ。これは
  (1) Containers 画面へページ遷移し、(2) 対象コンテナの詳細/ログパネルを開く、という2段階の操作になる。
- 本機能追加より前は、`MainWindow` が `NavigationViewModel` を自前で `new` して保持していた
  （Shell 固有の状態とみなしていた）。この構成では、`DashboardViewModel` がページ遷移を起こしても
  `MainWindow` が参照している `NavigationViewModel` インスタンスとは別物になり、`NavigationView` の
  選択状態や `Frame` の表示が同期しない。
- また、詳細/ログパネルを開く操作は `ContainersViewModel` が既に公開しているコマンド
  （`OpenDetailsCommand` / `OpenLogsCommand`）で行える。ダッシュボードから同じ画面状態を再現するには、
  Containers 画面が実際に使っているのと**同一の** `ContainersViewModel` インスタンスへ働きかける必要がある。
- 検討した代替案:
  - **`MainWindow` が `NavigationViewModel` を new し続ける**（却下）: 上記のとおりインスタンスが
    分裂し、ダッシュボードからの遷移が Shell の表示に反映されない。
  - **ダッシュボード専用のナビゲーション/オーケストレーション用の抽象を新設する**
    （`IContainerNavigationService` 等）（今回は見送り）: 疎結合になるが、Application/Presentation を
    跨ぐ新しい抽象の設計が必要で、現時点では過剰。まずは既存のDI基盤
    （[ADR-0010](0010-adopt-di-container-for-presentation.md)）の範囲で解く。

## Decision

`NavigationViewModel` を `Microsoft.Extensions.DependencyInjection` のシングルトンとして登録し、
`MainWindow` も `DashboardViewModel` も**同一インスタンス**を DI から解決して共有する。

- `App.xaml.cs`（Composition Root）で `AddSingleton<NavigationViewModel>()` を登録する。
  同様にダッシュボード全体の状態を保持する `DashboardViewModel` も `AddSingleton<DashboardViewModel>()` とする。
- `MainWindow` は `NavigationViewModel` を `new` せず、`Services.GetRequiredService<NavigationViewModel>()`
  で解決する。
- `DashboardViewModel` は、共有の `NavigationViewModel` と、Containers 画面と同一の `ContainersViewModel`
  （こちらも既存のDI登録で共有）をコンストラクタ注入で受け取り、稼働中コンテナ行の「詳細」「ログ」操作を
  「Containers へ遷移 → `ContainersViewModel.OpenDetails/OpenLogsCommand` を実行」の順で委譲する。

この方針は Issue #5 以降、複数の画面が同一のナビゲーション状態・画面状態を共有する必要がある箇所に適用する。

## Consequences

- ダッシュボードからのページ遷移が `MainWindow` の `NavigationView` 選択状態・`Frame` 表示と正しく同期する。
- ダッシュボードの行アクションが、Containers 画面が実際に表示するのと同一の `ContainersViewModel` に
  作用するため、遷移先で意図した詳細/ログパネルが開く。
- トレードオフ: `DashboardViewModel` が `ContainersViewModel` の具体型とその公開コマンドに依存する
  結合点が生まれる。両者がシングルトンである前提に依存しており、将来ViewModelのライフタイムや
  ページ活性化方式が変わると脆くなり得る。より疎な形（ナビゲーション/オーケストレーション用の抽象の導入）は
  将来の改善候補として [`docs/design/dashboard-view.md`](../design/dashboard-view.md) に記録する。
- シングルトン化により `NavigationViewModel` / `DashboardViewModel` はアプリ生存期間中インスタンスが保持される。
- 影響を受ける既存の決定・ドキュメント: [ADR-0010](0010-adopt-di-container-for-presentation.md)、
  [`docs/design/presentation-navigation.md`](../design/presentation-navigation.md)、
  [`docs/design/dashboard-view.md`](../design/dashboard-view.md)。
