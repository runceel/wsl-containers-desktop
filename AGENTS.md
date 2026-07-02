# AGENTS.md — WSL Containers Desktop 開発ガイド

このドキュメントは、GitHub Copilot（およびその他のAIコーディングエージェント）が
このリポジトリで作業する際の運用ルールをまとめたものです。

## プロジェクト概要

WSL Containers Desktop は、WinUI 3 / .NET C# で作る WSL (Windows Subsystem for Linux)
上のコンテナ管理用デスクトップアプリです。Docker Desktop と同等の機能セット
（コンテナ/イメージ/ボリューム/ネットワークの管理、ログ表示等）を目標としています。

> **現状:** このセッションではアプリ本体の実装は行わず、開発プロセス・Copilot運用ルールの
> 初期セットアップのみを行っています。WinUIプロジェクト本体の雛形作成は別セッションで着手します。

## プロジェクト構成方針（クリーンアーキテクチャ）

本プロジェクトは **クリーンアーキテクチャを意識した4層構成** を採用します
（詳細判断の経緯は [ADR-0005](docs/adr/0005-adopt-clean-architecture-layering.md)、
現在の構成スナップショットは [`docs/design/architecture-overview.md`](docs/design/architecture-overview.md) を参照）。

| 層 | 責務 | 依存してよい層 |
|---|---|---|
| Domain | エンティティ、値オブジェクト、ドメインルール | なし |
| Application | ユースケース、外部依存の抽象(interface) | Domain |
| Infrastructure | WSL/Docker連携、ファイルI/O等の具体実装 | Application, Domain |
| Presentation (WinUI) | XAML View、ViewModel(MVVM) | Application, Domain |

依存の向きは常に外側→内側（Presentation/Infrastructure → Application → Domain）。
逆方向の依存・層飛ばしの依存は禁止です。詳細は
[`.github/instructions/csharp.instructions.md`](.github/instructions/csharp.instructions.md) を参照。

**注意:** 2025年現在、実際の `.slnx`/各層の `.csproj` はまだ作成されていません
（方針のみ確定）。プロジェクト本体の着手時にこの節と `architecture-overview.md` を更新してください。

ソリューションファイルは、従来の `.sln` ではなく **`.slnx`**（XMLベースの新形式、.NET SDK 9.0.200以上で
`dotnet new sln` により生成）を採用します（[ADR-0006](docs/adr/0006-adopt-slnx-solution-file-format.md)）。

## 開発フロー（必須）

機能追加・仕様のある変更は、必ず以下の6フェーズを順に行います。
オーケストレーション手順の詳細は [`feature-workflow` skill](.github/skills/feature-workflow/SKILL.md) を参照。

1. **機能設計** — 何を作るか、スコープ、ユーザー価値を明確にする
2. **詳細設計** → `rubber-duck` agentでレビュー
3. **テスト作成**（MSTestで先に仕様をテストとして書く）→ `rubber-duck` agentでレビュー
4. **実装（厳密なTDD）** — Red → Green → Refactor を1振る舞いずつ反復
   ([`tdd-red`](.github/agents/tdd-red.agent.md) →
   [`tdd-green`](.github/agents/tdd-green.agent.md) →
   [`tdd-refactor`](.github/agents/tdd-refactor.agent.md))
5. **テスト** — 単体テストに加え、UIに関わる変更は既存の `winui-ui-testing` skillでE2E
6. **振り返り** → `rubber-duck` agentでレビュー。必要なら ADR / design doc を更新

単純な機械的修正（タイポ、フォーマット、挙動を変えないリネーム等）はこのフローの対象外です。
下記の「モデルルーティング」を参照してください。

### フェーズごとの完了条件 (Definition of Done)

`.github/skills/feature-workflow/SKILL.md` に一覧があります。要約:
設計フェーズはラバーダックの重大な指摘が解消済みであること、TDDフェーズは対象の振る舞いが
Refactor後もGreenを維持していること、振り返りフェーズはADR/design docが実装と一致していること。

## TDD（厳密なRed-Green-Refactor）

[ADR-0002](docs/adr/0002-adopt-strict-tdd-workflow.md) により、プロダクションコードの変更は
必ず「失敗するテストを先に書く」ことから始めます。詳細ルールは
[`.github/instructions/tests.instructions.md`](.github/instructions/tests.instructions.md)。

- テストフレームワークは **MSTest**（[ADR-0003](docs/adr/0003-select-mstest-as-unit-test-framework.md)）。
- 各フェーズは専用agentで実行し、フェーズの越境（例: Greenフェーズで新しいテストを書く）をしない。

## ADR (Architecture Decision Record)

設計判断・プロセス決定は [`docs/adr/`](docs/adr/README.md) にADRとして残します。

- 命名: `docs/adr/NNNN-kebab-case-title.md`（4桁連番）
- **不変性ルール**: 一度書いたADRの本文（Context/Decision/Consequences）は書き換えません。
  決定を覆す場合は新しいADRを追加し、古いADRの `Status` を `Superseded by ADR-YYYY` にするだけです。
- 実務手順は [`adr-workflow` skill](.github/skills/adr-workflow/SKILL.md) を参照。

現在のADR一覧は [`docs/adr/README.md`](docs/adr/README.md) を参照してください。

## 設計ドキュメント (`docs/design/`)

[`docs/design/`](docs/design/README.md) は常に**現在の姿だけ**を反映するスナップショットです。

- 過去の経緯・検討過程は書かない。理由が必要な場合はADRへのリンクのみ。
- 変更があったら追記ではなく**上書き**する。
- 実務手順は [`design-doc-maintenance` skill](.github/skills/design-doc-maintenance/SKILL.md) を参照。

## モデルルーティング（コスト最適化）

[ADR-0004](docs/adr/0004-adopt-model-routing-for-simple-changes.md) に基づき、作業の性質でモデルを使い分けます。

| 作業の性質 | 使うagent/モデル |
|---|---|
| タイポ修正、フォーマット、挙動を変えないリネーム等の機械的な小修正 | [`quick-fix` agent](.github/agents/quick-fix.agent.md)（`mai-code-1-flash-picker` 固定） |
| 設計、ラバーダック、TDD各フェーズ、ADR作成など判断を伴う作業 | 通常の高性能モデル（既定モデル） |
| どちらか迷う場合 | **必ず通常フロー側を選ぶ**（コスト削減より品質・仕様漏れ防止を優先） |

## 既存の再利用可能なskill/agent（重複させないこと）

以下はユーザーのCopilot CLI環境にすでにインストール済みです。ビルド・実行・E2E(UI)テスト・
パッケージング等の機能は、このリポジトリ独自には作らず、これらを利用してください。

### `winui` plugin（awesome-copilot marketplace）

- agent: `winui:winui-dev` — WinUI 3アプリの実装全般
- skills: `winui-dev-workflow`（ビルド/実行）, `winui-ui-testing`（E2E/UI自動テスト）,
  `winui-design`（Fluent Designルール）, `winui-code-review`, `winui-packaging`,
  `winui-wpf-migration`, `winui-setup`, `winui-session-report`

### `dotnet` plugin（awesome-copilot marketplace）

- skills: `csharp-scripts`, `dotnet-pinvoke`, `nuget-trusted-publishing`

### `dotnet/skills`（公式.NETチーム, marketplace短縮名 `dotnet-agent-skills`）

- `dotnet` plugin — C# LSP統合・高レベル.NET開発skill
- `dotnet-test` plugin — テスト実行/生成/カバレッジ/MSTestワークフロー（20 skills）
- `dotnet-msbuild` plugin — ビルド失敗診断・品質・最適化（18 skills）
- `dotnet-nuget` plugin — パッケージ管理

## セットアップ（他の開発者・新しい環境向け）

このリポジトリのCopilot運用には、上記のplugin/marketplaceが必要です。
未導入の環境では以下を実行してください（Copilot CLIのユーザー設定に対する変更のため、
リポジトリのクローンだけでは再現されません）。

```powershell
copilot plugin marketplace add dotnet/skills
copilot plugin install dotnet@dotnet-agent-skills
copilot plugin install dotnet-test@dotnet-agent-skills
copilot plugin install dotnet-msbuild@dotnet-agent-skills
copilot plugin install dotnet-nuget@dotnet-agent-skills
```

> 複数の `copilot plugin install` を同時並行で実行すると、marketplaceのgit clone処理が
> 競合し破損することがあります。**必ず1つずつ順番に**実行してください。

`winui`/`dotnet`（awesome-copilot marketplace）は個々の開発環境で別途導入済みである前提です。

## このリポジトリ独自のCopilot資産

| 種類 | 場所 | 用途 |
|---|---|---|
| Instructions | `.github/instructions/csharp.instructions.md` | C#コーディング規約・層間依存ルール |
| Instructions | `.github/instructions/xaml.instructions.md` | XAML/WinUI規約 |
| Instructions | `.github/instructions/tests.instructions.md` | MSTest規約・TDD各フェーズの許可/禁止事項 |
| Agent | `.github/agents/tdd-red.agent.md` | TDD: Redフェーズ専用 |
| Agent | `.github/agents/tdd-green.agent.md` | TDD: Greenフェーズ専用 |
| Agent | `.github/agents/tdd-refactor.agent.md` | TDD: Refactorフェーズ専用 |
| Agent | `.github/agents/adr-writer.agent.md` | ADR作成/更新支援 |
| Agent | `.github/agents/quick-fix.agent.md` | 機械的小修正専用（低コストモデル） |
| Skill | `.github/skills/feature-workflow/SKILL.md` | 6フェーズ開発フローのオーケストレーション |
| Skill | `.github/skills/adr-workflow/SKILL.md` | ADR作成・更新の実務手順 |
| Skill | `.github/skills/design-doc-maintenance/SKILL.md` | 設計ドキュメントのスナップショット更新手順 |

## スコープ外（今回未着手）

- WinUIプロジェクト本体の雛形作成（`.slnx`/各層の`.csproj`）
- 実際の機能実装・テストコード
- CI/CD（GitHub Actions）ワークフローの新規構築
