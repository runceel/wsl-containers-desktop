# 0005. クリーンアーキテクチャに基づく層分割を採用する

## Status

Accepted

## Context

- WSL Containers Desktop は Docker Desktop 相当の機能（コンテナ/イメージ/ボリューム/
  ネットワークの管理、WSL統合）を持つWinUI 3デスクトップアプリとして開発する。
- WSLやコンテナランタイムとの通信（インフラ層の詳細）と、アプリのユースケース・
  ビジネスルールを疎結合にし、以下を実現したい。
  - UIフレームワーク（WinUI 3）の詳細に引きずられずにユースケースを単体テストできる
    （ADR-0002のTDDを、外部依存の少ない層で高速に回せる）。
  - 将来的なUI刷新やコンテナランタイム実装の差し替え（例: 別のWSL API/CLIラッパー）に
    対する影響範囲を限定する。
- 本ADRの時点ではプロジェクト本体（`.sln`/各層の`.csproj`）はまだ作成しない
  （初期セットアップのスコープ外）。ここでは方針とルールのみを定める。

## Decision

クリーンアーキテクチャに基づき、以下の4層で構成する方針を採用する。

| 層 | 責務 | 依存してよい対象 |
|---|---|---|
| **Domain** | コンテナ/イメージ/ボリューム/ネットワーク等のエンティティ、値オブジェクト、ドメインルール | なし（何にも依存しない） |
| **Application** | ユースケース（アプリケーションサービス）、外部連携のための抽象（インターフェース） | Domain のみ |
| **Infrastructure** | WSL/コンテナランタイムとの実際の通信、ファイルI/O、設定の永続化など、Applicationで定義した抽象の実装 | Application, Domain |
| **Presentation** | WinUI 3のView・ViewModel（MVVM）、DI構成、アプリのエントリポイント | Application, Domain（Infrastructureへは実装の直接参照ではなくDI経由でのみ依存） |

- **依存方向のルール**: 依存は常に外側から内側（Presentation/Infrastructure → Application → Domain）
  に向かう。内側の層は外側の層の型・フレームワーク（WinUI, WSL API等）を一切参照しない。
- ViewModel は Application 層のユースケース/抽象に依存し、Infrastructure の実装クラスを
  直接 `new` しない（Presentation層のDI構成でのみ具象を解決する）。
- 各層はプロジェクト参照によって物理的にも分離する想定とする
  （実際の `.csproj` 分割は本ADRの適用範囲外で、着手時に別途スキャフォールドする）。
- 現時点の層構成の詳細（責務の具体例、ディレクトリ構成案）は
  [`docs/design/architecture-overview.md`](../design/architecture-overview.md) にスナップショットとして記載する。

## Consequences

- Domain/Application層は外部依存が薄いため、MSTest（ADR-0003）によるTDDを高速に回しやすい。
- WinUI 3固有の実装や、WSL/コンテナランタイムとの通信方式を変更する際の影響範囲が
  Infrastructure/Presentation層に限定される。
- 層を跨ぐたびにインターフェース定義が必要になり、小規模なうちは若干の冗長さが生じる。
  → `.github/instructions/csharp.instructions.md` に依存ルールを明記し、
    Copilotエージェントが層を跨いだ誤った依存を追加しないようにする。
- 実際のプロジェクト構成（`.sln`、各層の`.csproj`、参照設定）は、
  最初の機能実装に着手するタイミングで別途スキャフォールドする。
