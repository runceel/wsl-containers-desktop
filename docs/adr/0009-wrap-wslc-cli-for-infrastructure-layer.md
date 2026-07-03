# 0009. Infrastructure層で`wslc` CLIのプロセス起動ラップ方式を採用する

## Status

Accepted

## Context

- [Issue #4 [0001] コンテナ一覧・基本操作](https://github.com/runceel/wsl-containers-desktop/issues/4)
  の実装にあたり、Infrastructure層（[ADR-0005](0005-adopt-clean-architecture-layering.md)）で
  実際にWSL Containersと通信する方式を決める必要があった。
- [`docs/reference/wsl-containers-platform.md`](../reference/wsl-containers-platform.md)は、
  「WSL Container API（NuGetパッケージ, C#/WinRTネイティブ）」を第一候補、
  「`wslc.exe`プロセス起動によるラップ」を代替案としていた。
- 実機検証の結果、以下の事実を確認した。
  - NuGetパッケージ `Microsoft.WSL.Containers`（net8.0-windows対象, C#/WinRT projection）は
    `Session`/`Container`クラスを提供するが、公開APIは`CreateContainer`（自セッション内での新規作成）や
    `PullImage`等が中心で、**既存の全コンテナを列挙するAPI（`GetContainers`相当）が存在しない**。
    セッション内で自分が作成したコンテナを操作する用途のSDKであり、CLIや他ツールが作成した
    既存コンテナを一覧・監視する用途には向かない。
  - 一方 `wslc.exe` CLI（WSL 2.9.3のプレインストール環境で動作確認済み）は、
    `wslc list -a --format json` で全コンテナ（他ツール・CLI作成分を含む）をJSON配列として
    取得でき、`container start`/`container stop`/`container remove`等のライフサイクル操作も揃っている。
  - `wslc container remove`（`-f`無し）は実行中のコンテナに対して非0の終了コードと
    `stderr`への明確なエラーメッセージ（エラーコード`WSLC_E_CONTAINER_IS_RUNNING`付き）を返す。
  - `wslc`には`restart`サブコマンドが存在しない（`stop`→`start`の組み合わせで代替する必要がある）。
- 本Issueの受け入れ基準は「一覧表示中に、実際の状態（他ツールやCLIからの変更を含む）が反映される」
  ことを明示的に要求しており、SDKの`Session`ベースの設計ではこれを満たせない。

## Decision

`WslContainersDesktop.Infrastructure`層は、`Microsoft.WSL.Containers` NuGetパッケージ（C#/WinRT SDK）
ではなく、**`wslc.exe` CLIをプロセス起動でラップする方式**を採用する。

- コンテナの一覧取得・起動・停止・削除は、いずれも`System.Diagnostics.Process`で`wslc`を起動し、
  標準出力（JSON、またはID等）・標準エラー（エラーメッセージ）・終了コードを介して結果を得る。
- `restart`相当の操作は、Application層（`WslContainersDesktop.Application`）が
  `stop`→`start`のオーケストレーションとして実装する（Infrastructure層はCLIの1コマンド1操作に留める）。
- 将来、SDKが全コンテナ列挙APIを備える等仕様が変わった場合は、本ADRを覆す新しいADRを追加し、
  Infrastructure層の実装を差し替えることを想定する（Application層の`IContainerRuntimeClient`抽象
  自体は変更不要な設計とする）。

## Consequences

- 他ツール・CLIで作成/変更されたコンテナも一覧・操作の対象にでき、受け入れ基準を満たせる。
- CLIプロセスの起動オーバーヘッド（起動コスト、JSON/テキストパース）が発生するが、
  デスクトップGUIアプリのユーザー操作契機の頻度では許容範囲と判断する。
- CLIの出力フォーマット（JSON構造、エラーメッセージ文言、終了コード）が
  Public Preview中に変更される可能性がある。Infrastructure層にこれらの解析ロジックを閉じ込め、
  Application/Domain層への影響を防ぐ（[ADR-0005](0005-adopt-clean-architecture-layering.md)の層分割方針通り）。
- `restart`はCLIにネイティブな操作がないため、`stop`成功後`start`が失敗する等の部分失敗が
  起こりうる。Application層で最新状態の事前検証を行い、UI側は操作結果に応じて実際の状態に
  同期する設計とする（詳細は[`docs/design/`](../design/README.md)のコンテナ管理関連ドキュメントを参照）。
