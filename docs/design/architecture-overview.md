# アーキテクチャ概要

> このドキュメントは現時点のスナップショットです。経緯・検討過程は書きません。
> 採用理由は [ADR-0005](../adr/0005-adopt-clean-architecture-layering.md) を参照してください。

## 状態

`WslContainersDesktop.slnx`（[ADR-0006](../adr/0006-adopt-slnx-solution-file-format.md)）に、
以下の7プロジェクトが存在する。

| プロジェクト | 種別 | 内容 |
|---|---|---|
| `src/WslContainersDesktop.Domain` | classlib | エンティティ・値オブジェクト（現時点では空） |
| `src/WslContainersDesktop.Application` | classlib | ユースケース・抽象（現時点では空） |
| `src/WslContainersDesktop.Infrastructure` | classlib | WSL/コンテナランタイム連携の実装（現時点では空） |
| `src/WslContainersDesktop.App` | WinUI3 MSIXパッケージアプリ（net10.0-windows） | Presentation層。ナビゲーション基盤とローカライズ基盤を実装済み |
| `tests/WslContainersDesktop.Domain.Tests` | MSTest | Domain層の単体テスト（現時点では空） |
| `tests/WslContainersDesktop.Application.Tests` | MSTest | Application層の単体テスト（現時点では空） |
| `tests/WslContainersDesktop.App.Tests` | MSTest | Presentation層（ナビゲーション制御ロジック）の単体テスト |

Domain/Application/Infrastructureは、後続機能の実装を受け入れる空のプロジェクトとして存在する。
現時点で振る舞いを持つのはPresentation層のナビゲーション制御ロジックのみ。

## 層構成

```mermaid
flowchart TB
    Presentation["Presentation<br/>(WinUI 3 View / ViewModel, DI構成)"]
    Infrastructure["Infrastructure<br/>(WSL・コンテナランタイムとの通信, 永続化)"]
    Application["Application<br/>(ユースケース, 外部連携の抽象/インターフェース)"]
    Domain["Domain<br/>(エンティティ, 値オブジェクト, ドメインルール)"]

    Presentation --> Application
    Infrastructure --> Application
    Application --> Domain
```

依存は常に図の下向き（外側→内側）のみ。逆方向の依存（例: Domain が Infrastructure を参照する）は禁止。

## 各層の責務

### Domain

- コンテナ、イメージ、ボリューム、ネットワークなどのエンティティ・値オブジェクト。
- ドメインルール（例: 状態遷移の妥当性）。
- 外部フレームワーク（WinUI, WSL API等）への依存を一切持たない。

### Application

- ユースケース（例: 「コンテナを起動する」「イメージ一覧を取得する」）をアプリケーションサービスとして実装。
- Infrastructureが実装すべき抽象（インターフェース）をこの層で定義する
  （例: `IContainerRuntimeClient`）。
- Domainのみに依存する。

### Infrastructure

- WSL・コンテナランタイム（Docker Engine / containerd等、採用ランタイムは別途ADRで決定）との
  実際の通信を行うクライアント実装。
  - 具体的な統合対象は **WSL Containers**（`wslc` CLI / WSL Container API）。
    仕様サマリは [`docs/reference/wsl-containers-platform.md`](../reference/wsl-containers-platform.md) を参照。
- 設定やキャッシュの永続化（ファイルI/O、レジストリ等）。
- Applicationで定義された抽象を実装する。

### Presentation

- WinUI 3のView（XAML）とViewModel（MVVM、CommunityToolkit.Mvvm使用）。
- アプリのエントリポイントとDIコンテナ構成（Infrastructureの実装をApplicationの抽象へ束縛する）。
  現時点ではApplication/Infrastructureが空のため、DIコンテナは未導入。ViewModelは
  `MainWindow`から直接インスタンス化している。Infrastructureとの結合が必要になった時点で
  `Microsoft.Extensions.DependencyInjection`等の導入を検討する。
- ViewModelはApplication層のユースケース/抽象にのみ依存し、Infrastructureの具象クラスを直接参照しない。
- ナビゲーション基盤・ローカライズ基盤の詳細は
  [`docs/design/presentation-navigation.md`](presentation-navigation.md) を参照。

## テスト戦略との対応

- Domain / Application 層: MSTestによる高速な単体テスト（[ADR-0003](../adr/0003-select-mstest-as-unit-test-framework.md)）が主戦場。TDD（[ADR-0002](../adr/0002-adopt-strict-tdd-workflow.md)）はこの2層を中心に回す。
- Infrastructure層: 実際のWSL/コンテナランタイムとの結合部分。フェイク/モックを介した単体テストに加え、必要に応じ結合テストを検討する。
- Presentation層: ナビゲーション制御ロジック（ViewModel等）はMSTestの単体テストで検証し、
  実際の画面切り替え・起動/終了の挙動は`winui-ui-testing` skill（既存のwinui pluginが提供）による
  UIオートメーションテストで検証する。

## 今後の更新予定

- コンテナランタイムとの通信方式（WSL API直接呼び出し / CLIラッパー等）が決まり次第、
  Infrastructure層の節を具体化する。
- DIコンテナ導入時に、Presentation節の記述を更新する。
