# Hakonexa - WSL Containers Manager

Hakonexa - WSL Containers Manager は、WSL (Windows Subsystem for Linux) 上のコンテナを管理するための
WinUI 3 / .NET デスクトップアプリケーションです。Docker Desktop と同等の機能セット
（コンテナ/イメージ/ボリューム/ネットワークの管理、ログ表示等）を目標としています。

## 現状

Issue #3（土台: 空のWinUIウィンドウ起動）でプロジェクト本体（`.slnx`/各層の`.csproj`）を
スキャフォールドし、`NavigationView`によるプレースホルダ画面切り替えとローカライズ基盤を実装済みです。
実際のドメイン/ユースケースロジック（Domain/Application/Infrastructure層）はまだ空で、
今後の機能実装で積み上げていきます。

## 対象プラットフォーム: WSL Containers

本アプリは Microsoft Build 2026 で発表されたばかりの **WSL Containers**（`wslc` CLI /
WSL Container API, Public Preview）をGUIで管理するアプリです。仕様のサマリは
[`docs/reference/wsl-containers-platform.md`](docs/reference/wsl-containers-platform.md)
にまとめています（Public Preview中のため仕様が頻繁に変わる可能性があります）。

## アーキテクチャ

クリーンアーキテクチャを意識した4層構成（Domain / Application / Infrastructure /
Presentation）を採用しています。詳細は
[`docs/design/architecture-overview.md`](docs/design/architecture-overview.md)、
採用理由は [ADR-0005](docs/adr/0005-adopt-clean-architecture-layering.md) を参照してください。

## ドキュメント

| ディレクトリ | 内容 |
|---|---|
| [`docs/specs/`](docs/specs/README.md) | 個別機能の仕様書（何を作るか） |
| [`docs/design/`](docs/design/README.md) | 現在の設計の最新スナップショット |
| [`docs/adr/`](docs/adr/README.md) | 設計判断・プロセス決定の記録 (Architecture Decision Record) |
| [`docs/reference/`](docs/reference/README.md) | WSL Containers等、外部プラットフォームの仕様参照資料 |

## AIコーディングエージェントでの開発

本リポジトリは GitHub Copilot CLI 等のAIコーディングエージェントを用いた開発を前提としています。
エージェント向けの運用ルール（開発フロー、TDD、ADR運用、モデルルーティング等）は
[`AGENTS.md`](AGENTS.md) にまとめています。人間のコントリビューターが同じ開発フローに
従う場合も、そちらを参照してください。

## セットアップ（Copilot CLIでの開発に必要なplugin）

このリポジトリのCopilot運用には、以下のplugin/marketplaceが必要です。
未導入の環境では以下を実行してください（Copilot CLIのユーザー設定に対する変更のため、
リポジトリのクローンだけでは再現されません）。

```powershell
copilot plugin marketplace add dotnet/skills
copilot plugin install dotnet@dotnet-agent-skills
copilot plugin install dotnet-test@dotnet-agent-skills
copilot plugin install dotnet-msbuild@dotnet-agent-skills
copilot plugin install dotnet-nuget@dotnet-agent-skills
copilot plugin install microsoftdocs/mcp
```

> 複数の `copilot plugin install` を同時並行で実行すると、marketplaceのgit clone処理が
> 競合し破損することがあります。**必ず1つずつ順番に**実行してください。
>
> `microsoftdocs/mcp` はGitHubリポジトリからの直接インストール（`owner/repo`形式）です。
> CLIから「Direct plugin installs (repos, URLs, local paths) are deprecated」という警告が
> 出る場合がありますが、本執筆時点ではこの形式のみが利用可能です。marketplace経由の
> インストール方法が提供されたら、そちらに切り替えてください。

`winui`/`dotnet`（awesome-copilot marketplace）は個々の開発環境で別途導入済みである前提です。
