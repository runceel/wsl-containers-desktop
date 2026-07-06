# Presentation層: ダッシュボード（概要）画面

> このドキュメントは現時点のスナップショットです。経緯・検討過程は書きません。
> 採用理由は [ADR-0014](../adr/0014-dashboard-as-default-landing-and-first-nav-item.md) /
> [ADR-0015](../adr/0015-promote-navigation-viewmodel-to-di-singleton.md) を参照してください。

## 概要

`DashboardViewModel`（`ViewModels/DashboardViewModel.cs`）は、アプリの概要（ダッシュボード）画面を担う
ViewModel。各リソースのサマリ件数と、稼働中コンテナのリソース使用量を提示する。ダッシュボードは
`NavigationView` の先頭項目かつアプリ起動時の既定表示ページである
（[ADR-0014](../adr/0014-dashboard-as-default-landing-and-first-nav-item.md)）。

- Application層のInboundポート（`IContainerManagementService` / `IImageManagementService` /
  `IVolumeManagementService` / `INetworkManagementService`）にのみ依存し、`wslc` CLI の具体的な
  呼び出し方法には依存しない。
- 加えて、遷移導線のために共有の `NavigationViewModel` と、Containers 画面と同一インスタンスの
  `ContainersViewModel` をコンストラクタ注入で受け取る
  （いずれもDIシングルトン。[ADR-0015](../adr/0015-promote-navigation-viewmodel-to-di-singleton.md)）。
- `DashboardViewModel` 自身もDIシングルトンとして登録し、画面状態をアプリ生存期間中保持する。

## 画面構成

- **サマリ件数カード**: 稼働中コンテナ数（`RunningContainerCount`）、停止中コンテナ数
  （`StoppedContainerCount`）、イメージ数（`ImageCount`）、ボリューム数（`VolumeCount`）、
  ネットワーク数（`NetworkCount`）を表示する。件数はいずれも `int?` で、未取得・取得失敗時は `null` とし、
  UI では `—`（`FormatCount`）を表示する。
  - ネットワーク数はシステムネットワークも含めた総数を表示する。
- **稼働中コンテナのリソース使用量リスト**: `ContainerStats`（`DashboardContainerStatsRowViewModel` の
  `ObservableCollection`）を `ListView` で表示する。各行はコンテナ名（`Name`）、CPU使用率（`CpuText`、
  `"0.0 %"` 形式）、メモリ使用量（`MemoryText`、`"使用量 / 上限"` 形式）と、対象コンテナの
  「詳細」「ログ」を開くボタンを持つ。
- **空状態**: `IsStatsEmpty`（`!IsStatsLoading && ContainerStats.Count == 0 && StatsErrorMessage is null`）が
  真のとき、稼働中コンテナがない旨の空状態テキストを表示する。読み込み中は空状態を表示しない。
- **読み込み中表示**: `IsStatsLoading` が真のあいだは、リソース使用量リストを隠して `ProgressRing` と
  「読み込み中」テキストを表示する。`Refresh` で一覧を一旦クリアしても空状態が点滅せず、更新中であることが
  ユーザーに分かる。

## 更新とエラー表示（部分失敗の許容）

- `RefreshAsync`（`RefreshCommand`）は、5種類の情報（コンテナ件数・イメージ件数・ボリューム件数・
  ネットワーク件数・稼働中コンテナのリソース使用量）を**それぞれ独立した `try`/`catch` で**取得する。
  一部の取得が失敗しても、成功した他の情報は更新して表示する。
- 各セクションは専用のエラーメッセージ（`ContainerCountErrorMessage` / `ImageCountErrorMessage` /
  `VolumeCountErrorMessage` / `NetworkCountErrorMessage` / `StatsErrorMessage`、いずれも `string?`）を持ち、
  対応する `IsXxxErrorVisible` / `IsXxxCountErrorVisible` で表示可否を導出する。取得成功時は件数を、
  失敗時は該当セクションにエラーを表示する。
- 稼働中/停止中のコンテナ件数は**同一のコンテナ取得を共有**する。そのため取得失敗時の
  `ContainerCountErrorMessage` は稼働中カード・停止中カードの**両方**に表示する。片方だけに出すと、
  失敗した停止中カードの `—` が「未取得」と区別できなくなるため。
- `IsRefreshing` による再入ガードを持つ。`RefreshAsync` は最初の `await` より前に
  `if (IsRefreshing) return;` で二重実行を弾き、`finally` で解除する（CommunityToolkit の
  `AsyncRelayCommand` は再入時に自動でノーオペにはならないため、手動でガードする）。
- ページの `Loaded` で `RefreshCommand` を実行し、初回表示時に取得を開始する。
- **読み込み中フラグ**: `RefreshAsync` は `try` の先頭で `IsStatsLoading = true` にし、統計取得の
  `finally` で `false` に戻す。リソース使用量リストは取得のたびに一旦クリアされるため、読み込み中は
  リストを隠して `ProgressRing`（「読み込み中」）を表示し、空状態（`IsStatsEmpty`）は抑制する。
  `IsStatsLoading` は `IsStatsEmpty` の再評価を通知する。

## ナビゲーション導線

- 各サマリ件数カードはボタンであり、押下すると対応する一覧画面へ遷移する
  （`ShowContainersCommand` / `ShowImagesCommand` / `ShowVolumesCommand` / `ShowNetworksCommand`。
  稼働中・停止中コンテナのカードはいずれも Containers へ遷移する）。遷移は共有の
  `NavigationViewModel.NavigateToCommand` に委譲する。
- 稼働中コンテナ行の「詳細」「ログ」ボタンは、`OpenContainerDetailsCommand` /
  `OpenContainerLogsCommand` により「Containers 画面へ遷移 → 共有の `ContainersViewModel` の
  `OpenDetailsCommand` / `OpenLogsCommand` を対象コンテナIDで実行」の順で委譲し、遷移先で対象の
  詳細/ログパネルを開く。

## 実装上の留意点

- **行アクションはコマンドバインドではなく `Click` ハンドラ**: 稼働中コンテナ行の「詳細」「ログ」
  ボタンは、仮想化された `ListView.ItemTemplate` が独立した namescope になり、ページレベルの
  コマンドへ `x:Bind`/`ElementName` で到達できないため、`Click` ハンドラ + `CommandParameter="{x:Bind}"`
  でコードビハインドから `DashboardViewModel` のコマンドへ委譲する（既存の Containers 画面と同じ方式）。
- **行ボタンの `AutomationId`**: 行の「詳細」「ログ」ボタンには、行の `ContainerId` から一意な
  `AutomationId` を `AutomationIdConverter` で付与する（既存の Containers 画面と同じパターン）。
- **ナビゲーション制御ロジックのテスト**: `DashboardViewModel` の単体テストでは、遷移が観測可能に
  なるよう初期ページを中立的なキー（`NavigationPageKey.Settings`）に置いてから検証する。アプリ実行時の
  既定初期ページ（`Dashboard`）とは別で、テスト都合の初期値である。

## 既知の制約・今後の改善候補

現時点の実装で受容している制約と、将来の改善候補（現時点ではスコープ外）を記録する。

- **リソース使用量のパースは寛容**: `wslc stats` のCPU/メモリ文字列が想定形式で解析できない場合、
  現在は `0`（`0.0 %` / `0 B`）として扱う。実機の実出力は想定形式であることを確認済みだが
  （[`docs/reference/wsl-containers-platform.md`](../reference/wsl-containers-platform.md)）、Public Preview の
  `wslc` が単位やフィールドを変えると、誤った「0」を実データのように表示し得る。今後は未知形式を
  パース失敗（`ContainerRuntimeException`）として扱い、統計セクションのエラーとして可視化することを検討する。
- **`ContainersViewModel` への直接結合**: 行アクションが `ContainersViewModel` の具体型と公開コマンドに
  依存している（[ADR-0015](../adr/0015-promote-navigation-viewmodel-to-di-singleton.md)）。将来的には
  ナビゲーション/オーケストレーション用の抽象（例: `IContainerNavigationService.OpenDetails/OpenLogs`）で
  疎結合化する余地がある。
- **取得は逐次実行**: 5種類の取得は独立しているが逐次に行う。起動直後のレイテンシが問題になる場合は、
  `wslc` の並行起動の可否を確認のうえ、独立取得の並行化を検討する。
