# 0017. `ContainersViewModel` とランタイムクライアントを機能単位のコンポーネントへ分割する

## Status

Accepted

## Context

- [Issue #48 ContainersViewModel とランタイムクライアントの責務を分割する](https://github.com/runceel/wsl-containers-desktop/issues/48)
  の時点で、`ContainersViewModel`（Presentation層）は約1,000行に達し、一覧、行操作、詳細、ログ、
  対話シェル、セッション寿命（`ContainersPage`/`LogsWindow`/`ShellWindow`のポップアウト共有状態を含む）
  を単一クラスで管理していた。
- `IContainerRuntimeClient` / `WslcCliContainerRuntimeClient`（Application/Infrastructure層）は、
  コンテナ、イメージ、ボリューム、ネットワーク、統計、ログ、execをすべて1つの抽象・1つの実装で扱っていた。
- これらは変更影響範囲が広く、状態遷移（一覧の差分更新、ログ/execストリームの寿命、ポップアウト
  ウィンドウとの状態共有）を個別に理解・テストしづらいという問題があった。
- `ContainersViewModel`は`ContainersPage`・`LogsWindow`・`ShellWindow`から共有されるDIシングルトンであり、
  XAMLバインディング（プロパティ名）・生成済みコマンド名（`[RelayCommand]`由来）を変更すると、
  3画面のXAML/バインディングとテストに影響が及ぶ。
- 検討した代替案:
  - **`ContainersViewModel`自体を一覧/詳細/ログ/シェルの複数ViewModelへ分割し、各画面が個別のDI登録を
    受け取る**（却下）: [ADR-0015](0015-promote-navigation-viewmodel-to-di-singleton.md)が確立した
    「3画面が同一インスタンスの状態を共有する」前提が崩れ、ポップアウト（`LogsWindow`/`ShellWindow`）が
    メイン画面と独立したViewModelを持つことになり、選択中コンテナやログ購読状態の同期が破綻する。
  - **Infrastructureの実装のみ分割し、Applicationの`IContainerRuntimeClient`は単一抽象のまま残す**
    （却下）: Infrastructure側は機能単位に分離できても、Application層のテスト・モックが依然全機能を
    横断する1つのフェイクに依存し、テストの焦点が絞れない。ADR-0009が「Application層の
    `IContainerRuntimeClient`自体は変更不要」と想定していたが、Issue #48でのSRP・テスト容易性の要求は
    ポートの分割を必要とする。

## Decision

- `ContainersViewModel`は、`ContainersPage`・`LogsWindow`・`ShellWindow`が共有するDIシングルトンの
  公開XAML/コマンドファサードとして維持しつつ、内部を一覧/詳細/ログ/シェルの機能単位の
  Presentation構成コンポーネントへ分割する。ファサードは手書きの委譲プロパティで各コンポーネントへ
  転送し、子コンポーネントの`PropertyChanging`/`PropertyChanged`を同名で再発行することで、
  既存の生成済みコマンド名とポップアウト間の共有状態（同一インスタンスであること）を保つ。
- Application層のアウトバウンド抽象`IContainerRuntimeClient`を廃止し、以下8つの機能単位ポートへ
  分割する: `IContainerQueryClient`（一覧/詳細取得）、`IContainerLifecycleClient`
  （起動/開始/停止/削除）、`IContainerLogClient`（ログのスナップショット/フォロー）、
  `IContainerExecClient`、`IContainerStatsClient`、`IImageRuntimeClient`、`IVolumeRuntimeClient`、
  `INetworkRuntimeClient`。Application層のインバウンドのサービス契約（`ContainerManagementService`等）
  は変更しない。`restart`はADR-0009の方針を継続し、Application層で`stop`→`start`として実装する。
- Infrastructure層の`WslcCliContainerRuntimeClient`を廃止し、上記の各アウトバウンドポートに対応する
  単一責務の実装クラスへ分割する。CLIプロセス起動・終了コード/エラー変換・JSON配列のデシリアライズ・
  ストリーム例外変換・対話的セッションのオープンといった共通処理は`WslcCliCommandExecutor`に集約し、
  各実装クラスはリソースのマッピング/パースに専念する。ボリューム/ネットワークの検査は
  境界を設けた並行実行（bounded concurrency）を維持する。
- 本決定は、ユーザーから見える挙動・XAMLバインディングの形・CLIへ渡す引数・`wslc`統合戦略
  （ADR-0009）を変更しない。

### ADR-0009との関係

ADR-0009の「`wslc.exe` CLIをプロセス起動でラップする」という中核決定は本ADRでも有効であり、
`Status`は変更しない。ただし、ADR-0009の Decision（40〜41行目）は「Application層の
`IContainerRuntimeClient`抽象自体は変更不要」と想定していた。本ADRは、Issue #48で明らかになった
単一責任・テスト容易性の要求に基づき、**その抽象の形（ポート分割の有無）に関する想定を修正する**もので
あり、ADR-0009本文は書き換えず、CLIラップという統合戦略そのものを置き換えるものではない。

## Consequences

- `ContainersViewModel`は公開APIの形（プロパティ名・コマンド名）を保ったまま、一覧/詳細/ログ/シェルの
  内部実装をそれぞれ独立してテスト・変更できるようになる。
- Application/Infrastructureの各ポート・実装が機能単位になり、変更影響範囲とテストの焦点が絞られる。
  一方で、ポート数が1つから8つに増えるため、DI登録（`App.xaml.cs`）とコンストラクタ注入の対象が
  増える。
- 共通のCLI実行・エラー変換ロジックを`WslcCliCommandExecutor`に集約することで、機能単位の実装間で
  コードの重複を避ける。
- `ContainersViewModel`が手書きの委譲プロパティと`PropertyChanging`/`PropertyChanged`の再発行に
  依存するため、新しいプロパティを子コンポーネントに追加する際は、ファサード側の転送コードを
  追加し忘れないよう注意が必要になる。
- 影響を受ける既存ドキュメント: [ADR-0009](0009-wrap-wslc-cli-for-infrastructure-layer.md)
  （Statusは変更しないが、本ADRから参照される）、[ADR-0005](0005-adopt-clean-architecture-layering.md)、
  [ADR-0010](0010-adopt-di-container-for-presentation.md)。`docs/design/`配下の関連ドキュメント
  （アーキテクチャ概要、Containers画面、Presentationナビゲーション等）は、実装完了後に
  最新のクラス構成へ更新する。
