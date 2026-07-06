# WSL Containers プラットフォーム仕様サマリ

> **最終確認日: 2026-07-03**（Microsoft Build 2026 発表直後の情報。**Public Preview** のため
> 仕様は変わりやすい。実装前に必ず一次情報源または Microsoft Learn MCP で最新情報を確認すること）

本アプリ（WSL Containers Desktop）は、この「WSL Containers」機能をGUIで管理するための
Docker Desktop相当のアプリである。つまり本アプリの Infrastructure層（[ADR-0005](../adr/0005-adopt-clean-architecture-layering.md)）は、
ここに書かれている `wslc` CLI または WSL Container API のいずれか（両方）をラップして実装することになる。

## 一次情報源

- 発表記事: [WSL container is now available for public preview](https://devblogs.microsoft.com/commandline/wsl-container-is-now-available-for-public-preview/)（Microsoft Command Line devblog, 2026年6月）
- ドキュメントハブ: https://aka.ms/wslc
- APIリファレンス: https://wsl.dev/api-reference/
- GitHubリポジトリ（サンプル・リリース）: https://github.com/microsoft/WSL
- WSLリリース（最新pre-release）: https://github.com/microsoft/WSL/releases
- Build 2026 セッション資料（デモ・Copilotプロンプト集）: https://github.com/microsoft/Build26-DEM346-whats-new-in-windows-subsystem-for-linux

## 何が発表されたか

Microsoft Build 2026 で、WSL (Windows Subsystem for Linux) に **WSL Containers** という新機能が
発表された。サードパーティ製ツール（Docker Desktop等）なしに、WindowsからLinuxコンテナ
（OCI/Docker形式のイメージ）をネイティブに作成・実行・管理できるようにするもの。

- **ステータス**: Public Preview。WSL 2.9.3 以降の pre-release に含まれる。
  - 更新方法: `wsl --update --pre-release`
- **GA予定**: 2026年秋（Fall 2026）予定とアナウンスされている。

## 構成要素

### 1. `wslc.exe` CLI

- WSL更新後に `PATH` に追加される新しいバイナリ。
- Docker CLIとほぼ同じ使用感（既存のDocker CLIの知識・スクリプトがほぼそのまま使える）。
- `container` / `container.exe` という別名（alias）でも同じバイナリが呼び出せる。
- 例:
  ```powershell
  # Linuxデスクトップ環境をコンテナで起動し接続する例
  wslc run -d --name=webtop -e PUID=1000 -e PGID=1000 -e TZ=Etc/UTC -p 3000:3000 -p 3001:3001 lscr.io/linuxserver/webtop:ubuntu-kde

  # GPUアクセスを確認する例（PyTorch + CUDA）
  wslc run --rm --gpus all pytorch/pytorch:2.5.1-cuda12.4-cudnn9-runtime python -c "import torch; print(torch.cuda.is_available())"
  ```

#### `wslc stats`（稼働中コンテナのリソース使用量）— 実機確認済み（wslc 2.9.3.0）

- 実機（wslc `2.9.3.0`, 2026-07-03 確認）で検証した事実。本アプリのダッシュボードが利用する。
- **`--no-stream` オプションは存在しない**。`stats` は常にスナップショット（1回分）を返すため、
  Docker CLI の `docs stats --no-stream` に相当する指定は不要。誤って付けると `wslc` は
  終了コード 1 で失敗する。
- 正しいコマンドは `wslc stats --format json`（`--format` は `json` / `table`、既定は `table`）。
  その他のオプションは `-a`/`--all`、`--no-trunc`。
- 出力は**JSON配列**で、各要素は以下のキーを持つ（値はすべて文字列）:
  ```json
  [
    {
      "BlockIO": "0 B / 0 B",
      "CPUPerc": "0.00%",
      "ID": "…",
      "MemPerc": "0.01%",
      "MemUsage": "1.82 MiB / 15.37 GiB",
      "Name": "wcd-demo-idle",
      "NetIO": "0 B / 0 B",
      "PIDs": "3"
    }
  ]
  ```
  - メモリは `使用量 / 上限` を**空白区切り**（例: `"1.82 MiB / 15.37 GiB"`）で返す。
    単位は2進接頭辞（`MiB`/`GiB` 等）。CPUは末尾に `%` が付く（例: `"0.00%"`）。
  - 本アプリが利用するのは `ID` / `Name` / `CPUPerc` / `MemUsage` のみ。
    `BlockIO` / `MemPerc` / `NetIO` / `PIDs` は現状は使わない。

### 2. WSL Container API（本アプリが主に統合すべき対象）

- NuGetパッケージとして配布（nuget.org および WSLリリースページ）。
- **C, C++, C# をサポート**（本アプリはWinUI 3 / C#なので、Infrastructure層からこのAPIを
  直接利用する形が第一候補になる。`wslc.exe` をプロセス起動でラップする方式は代替案）。
- MSBuild / CMake とも統合可能（プロジェクトファイルへの数行の追加でコンテナのビルド・
  デプロイをアプリのビルドプロセスに組み込める）。
- 詳細は [APIリファレンス](https://wsl.dev/api-reference/) およびリポジトリ内サンプル
  （`doc/samples/` 配下、例: `WSLC-CustomContainer`）を参照。

### 3. 基盤の改善（WSL Containers限定、将来WSL全体に展開予定）

- 既定ファイルシステム: `virtiofs`（Windowsファイルアクセスが従来比2倍高速）。
- 既定ネットワーキングモード: `consomme`（Linux側の通信をWindows側にリレーし、
  VPN/プロキシ等との互換性を改善する実験的モード）。
- メモリ回収の改善（未使用時にWindowsホストへメモリを段階的に返却）。

### 4. エンタープライズ向け機能

- GPO/ADMX ポリシーによる制御（利用可能なdistro/コンテナイメージの制限等）。
- Intune管理（ダッシュボード対応は追って提供予定）。
- Microsoft Defender for Endpoint (MDE) のWSLプラグインがコンテナイベントにも対応（private preview）。
- レジストリのアローリスト機能（許可するコンテナレジストリを組織単位で制限）。

### 5. 関連ツールとの統合

- VS Code Dev Containers が `wslc` に対応（`0.462.0-pre-release`、Dev Container設定の
  "Docker Path" を `wslc` に変更することで利用可能）。
- Docker Desktop / Podman Desktop / Rancher Desktop など既存ツールも、今回の基盤改善
  （virtiofs, consomme等）の恩恵を受ける（＝本アプリの直接の競合であり比較対象になり得る）。

## 本アプリの設計への示唆（今後の詳細設計フェーズで検討）

- Infrastructure層の実装方式は「WSL Container API（NuGet, C#ネイティブ）」を第一候補とし、
  「`wslc.exe` プロセス起動によるラップ」を代替案として比較検討する
  （実際にどちらを採用するかはADRとして別途記録する。本ドキュメントは事実の要約に留める）。
- Public Previewである以上、API仕様が変わる可能性を前提に、Infrastructure層の抽象
  （`IContainerRuntimeClient` 等, [ADR-0005](../adr/0005-adopt-clean-architecture-layering.md)参照）で
  しっかり隔離し、API変更の影響がApplication/Domain層に波及しないようにする。
- GPU対応、GPOによる制御など、Docker Desktopにはない/異なる特有機能があるため、
  機能設計フェーズで「Docker Desktop相当」に単純に倣うだけでなく、これらの差分を洗い出すこと。

## 更新履歴の考え方

このドキュメントは `docs/reference/` の運用ルールに従い、情報が古くなったら**上書き更新**する
（経緯は書かない）。更新したら冒頭の「最終確認日」も更新すること。
