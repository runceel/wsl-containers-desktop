# 0006. C#ソリューションファイル形式として .slnx を採用する

## Status

Accepted

## Context

- 本プロジェクト本体（`.sln`/各層の`.csproj`）は [ADR-0005](0005-adopt-clean-architecture-layering.md) の
  時点ではまだ作成されておらず、着手時に別途スキャフォールドする方針だった。
- 従来の `.sln` はカスタムのテキスト形式で、差分が読みにくく、マージコンフリクトが起きやすい。
- .NET SDK 9.0.200 以降、XMLベースの新しいソリューションファイル形式 **SLNX**（`.slnx`）が
  `dotnet` CLI でサポートされている（`dotnet new sln` での新規作成、`dotnet sln migrate` での
  既存 `.sln` からの変換）。Visual Studio 17.14 以降でも安定サポートされている。
- .NET 10 SDK 以降では `dotnet new sln` が既定で `.slnx` を生成するようになり、
  `.sln` は将来的にレガシー形式という位置付けになっていく見込み。

## Decision

- 本プロジェクトのC#ソリューションファイルは、従来の `.sln` ではなく **`.slnx`** を採用する。
- スキャフォールド時は `dotnet new sln` で `.slnx` を新規作成する
  （利用する .NET SDKが9.0.200未満で `.sln` が生成される場合は `dotnet sln migrate` で変換する）。
- クリーンアーキテクチャの層構成・依存ルール自体（ADR-0005）には変更はない。
  本ADRはソリューション**ファイル形式**のみを対象とする。

## Consequences

- ソリューションファイルがXMLベースになり、可読性・diffの追いやすさが向上する。
- `.slnx` を前提とするため、開発者・CI環境ともに .NET SDK 9.0.200 以上が必須になる
  （既存の `.sln` ツール連携が残っている外部ツールがある場合は個別に確認する）。
- プロジェクト本体スキャフォールド時、[`docs/design/architecture-overview.md`](../design/architecture-overview.md)
  および [`AGENTS.md`](../../AGENTS.md) の該当箇所を `.slnx` を前提とした記述に更新する。
