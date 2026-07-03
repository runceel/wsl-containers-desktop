# 0010. PresentationにDIコンテナ(Microsoft.Extensions.DependencyInjection)を導入する

## Status

Accepted

## Context

- [`docs/design/presentation-navigation.md`](../design/presentation-navigation.md)は、
  「`Application`/`Infrastructure`が空のためDIコンテナは未導入。`MainWindow`が
  `NavigationViewModel`を直接`new`する。Infrastructureとの結合が必要になった時点で
  `Microsoft.Extensions.DependencyInjection`等の導入を検討する」としていた。
- [Issue #4 [0001] コンテナ一覧・基本操作](https://github.com/runceel/wsl-containers-desktop/issues/4)
  により、初めてInfrastructure層（[ADR-0009](0009-wrap-wslc-cli-for-infrastructure-layer.md)）と
  Application層に実装が入り、PresentationのViewModel（`ContainersViewModel`）が
  `IContainerManagementService`（Application層の抽象）に依存する必要が生じた。
- [ADR-0005](0005-adopt-clean-architecture-layering.md)は「ViewModelはInfrastructureの実装クラスを
  直接`new`しない（Presentation層のDI構成でのみ具象を解決する）」と定めており、
  Presentation側で具象クラス（`WslcCliRunner`、`WslcCliContainerRuntimeClient`、
  `ContainerManagementService`）を組み立てて注入する仕組みが必要になった。
- WinUI 3の`Frame.Navigate(Type)`によるページ遷移は、対象ページがパラメーターレスの
  コンストラクタを持つことを要求する（[`NavigationPageRegistry`](../design/presentation-navigation.md)は
  `Type`ベースの遷移を採用済み）。そのため、ページ自身が依存解決の起点（Service Locator的な参照）を
  持つ必要がある。

## Decision

`WslContainersDesktop.App`（Presentation層）に`Microsoft.Extensions.DependencyInjection`を導入する。

- `App`（`App.xaml.cs`）をComposition Rootとし、コンストラクタで`ServiceCollection`を構築して
  以下を登録する。
  - `IWslcCliRunner → WslcCliRunner`（Infrastructure）
  - `IContainerRuntimeClient → WslcCliContainerRuntimeClient`（Infrastructure）
  - `IContainerManagementService → ContainerManagementService`（Application）
  - `ContainersViewModel`（Presentation、シングルトン。トップレベルページは`NavigationViewModel`と
    同様にアプリケーションライフタイムで1インスタンスのため）
  - 構築した`ServiceProvider`を`public IServiceProvider Services { get; }`として`App`に公開する。
- `ContainersPage`はパラメーターレスのコンストラクタ内で
  `((App)Application.Current).Services.GetRequiredService<ContainersViewModel>()`により
  ViewModelを解決する。
- `WslContainersDesktop.App.csproj`に`WslContainersDesktop.Infrastructure`への
  プロジェクト参照を追加する。これはDI登録（Composition Root）のためであり、
  ViewModelやView本体がInfrastructureの具象型を直接参照するわけではないため、
  [ADR-0005](0005-adopt-clean-architecture-layering.md)の「DI経由の抽象参照はOK」に整合する。
- `NavigationViewModel`（依存を持たないため`MainWindow`が直接`new`する既存方式）は、
  本ADRの対象外とし、当面現状の実装のままとする。

## Consequences

- ViewModel（`ContainersViewModel`）は`IContainerManagementService`という抽象にのみ依存し、
  MSTestでのフェイクによる単体テストが可能になる。
- Composition Root（`App.xaml.cs`）に依存解決の詳細が集約され、Infrastructureの実装差し替え
  （例: [ADR-0009](0009-wrap-wslc-cli-for-infrastructure-layer.md)が将来覆された場合）の影響範囲を
  `App.xaml.cs`のDI登録部分に限定できる。
- [`docs/design/presentation-navigation.md`](../design/presentation-navigation.md)の
  「DIコンテナは未導入」という記述は本ADRの実装をもって古くなるため、
  実装完了後に当該ドキュメントをスナップショットとして更新する
  （[`design-doc-maintenance`](../../.github/skills/design-doc-maintenance/SKILL.md) skill使用）。
