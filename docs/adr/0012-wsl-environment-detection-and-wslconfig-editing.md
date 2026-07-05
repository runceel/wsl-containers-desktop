# 0012. WSL環境検出と`.wslconfig`編集の方式を採用する

## Status

Accepted

## Context

- [Issue #7 [0007] 設定画面](https://github.com/runceel/wsl-containers-desktop/issues/7)
  で、(1) WSL連携状態（WSL Containersの動作要件を満たすか）の確認と、
  (2) リソース制限（メモリ量・論理プロセッサ数）の `.wslconfig` への保存を実装する必要がある。
- [`docs/reference/wsl-containers-platform.md`](../reference/wsl-containers-platform.md) より、
  WSL ContainersはWSL 2.9.3以降のプレリリース版と `wslc.exe` を前提とする。
- Microsoft Learnで確認した事実:
  - `wsl --version` は「WSL version: X.Y.Z.W」形式でバージョンを標準出力に出す。
  - `.wslconfig` はINI形式で、`[wsl2]` セクションに `memory`（`4GB`/`512MB` 等のサイズ表記）、
    `processors`（整数）、`swap` 等を持つ。未指定時はWSLの既定（メモリはホストの50%、
    プロセッサは全論理コア）が使われる。
  - ユーザーが手書きしたコメントや、本アプリが扱わない他セクション・他キーが存在しうる。
- クリーンアーキテクチャ（[ADR-0005](0005-adopt-clean-architecture-layering.md)）に従い、
  「WSL 2.9.3以上を要件とする」という**しきい値ポリシーはApplication層が所有**し、
  Infrastructure層は外部ツールの生の出力を解析・提供する役割に留めるべきである。
- 外部プロセス起動・ファイルI/Oの実体（thin edge）は単体テストの対象外とし、解析・組み立ての
  ロジックはフェイクのseam経由でテスト可能にする方針（[ADR-0009](0009-wrap-wslc-cli-for-infrastructure-layer.md)
  で確立したパターン）を踏襲する。

## Decision

### WSL環境検出

- Application層の送出ポート `IWslEnvironmentProbe` は、環境の**生の事実**
  `WslEnvironmentInfo(string? WslVersion, bool IsWslContainersAvailable)` を返す（要件判定は行わない）。
- Infrastructure層 `WslEnvironmentProbe` は次の低レベルseamに依存し、解析ロジックを担う（単体テスト対象）:
  - `IWslCommandRunner`（`wsl.exe` を起動し `CliResult` を返す。実体は薄く、テスト対象外）で
    `wsl --version` を実行し、標準出力から「WSL version: X.Y.Z(.W)」を抽出する。取得できない/
    プロセス失敗時は `WslVersion=null`（＝未検出）とする。
  - `IWslcExecutableProbe`（PATH上に `wslc.exe` が存在するか）で `IsWslContainersAvailable` を判定する。
- Application層 `SettingsService` が要件判定ポリシーを所有する。
  最小要件バージョンを `MinimumWslContainersVersion = 2.9.3` とし、
  `IsWslContainersAvailable && (解析済みバージョン >= 最小要件)` を満たすときのみ
  `WslIntegrationStatus.MeetsRequirements = true` とする。
- 要件を満たさないときは、Presentation層でのUI無効化に加え、`SettingsService` の保存・リセット
  でも要件を再チェックし、満たさない場合は `WslRequirementsNotMetException` を送出する
  （UI無効化とサービス側再検証の二重の安全策）。

### `.wslconfig` 編集

- 送出ポート `IWslResourceLimitsStore` を通じて読み書きする。実体 `WslConfigResourceLimitsStore` は
  ファイルI/Oを `IWslConfigFileAccessor`（`Environment.GetFolderPath(SpecialFolder.UserProfile)` 配下の
  `.wslconfig` を読み書き。実体は薄くテスト対象外）に委譲し、INIの解析・組み立てロジックを担う
  （単体テスト対象）。
- 読み取り: `[wsl2]` セクションの `memory`・`processors` を取得する。`memory` は `4GB`/`512MB`/
  小文字単位などを解釈しメガバイト単位（`int`）へ正規化する。ファイルが存在しない・`[wsl2]` が無い・
  値が無い場合は「未指定（WSL既定）」として扱う。
- 書き込み: **他セクション・他キー・コメント・改行を保全する**。すべての `[wsl2]` セクション内の
  既存 `memory`/`processors` 行を除去したうえで、先頭の `[wsl2]`（無ければ新規作成）へ新しい値を
  挿入する（重複による古い値の残存を防ぐ）。`memory` は常に `<n>MB` 形式で書き出し、値が
  未指定（null）のキーは書き出さない（＝オーバーライドを外す）。
- セクション名・キー名は大文字小文字を区別せず照合し、`=` 周辺の空白を許容する。
- 書き込みは一時ファイルへ書いてから置換する原子的書き込みとし、途中失敗による設定破損を防ぐ。

## Consequences

- 要件判定（2.9.3しきい値）がApplication層に集約され、Infrastructureは外部ツールの出力解析に専念する
  ため、しきい値やポリシー変更の影響範囲が明確になる。
- `wsl --version` の成否と `wslc` のPATH存在を独立した信号として組み合わせるため、
  「WSLはあるがWSL Containersが未導入」といった状態も要件未達として扱える。
- `.wslconfig` の編集がユーザーの既存記述（コメント・他セクション）を壊さないため、手書き設定との
  共存が可能になる。一方、`memory` は常にMB表記へ正規化されるため、ユーザーが `4GB` と書いていた
  値が保存操作で `4096MB` に変わりうる（意味は等価）。
- `wsl --version` / `.wslconfig` のフォーマットはPublic Preview中に変わりうるが、解析ロジックを
  Infrastructure層に閉じ込めることで上位層への影響を防ぐ（[ADR-0005](0005-adopt-clean-architecture-layering.md)）。
- 設定反映に必要なWSL再起動は自動実行しない（[ADR-0011](0011-do-not-auto-restart-wsl-on-settings-change.md)）。
