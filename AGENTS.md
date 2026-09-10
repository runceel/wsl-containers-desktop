# AGENTS.md — WSL Containers Desktop 開発ガイド

このドキュメントは、GitHub Copilot（およびその他のAIコーディングエージェント）が
このリポジトリで作業する際の運用ルールをまとめたものです。プロジェクトの概要・人間の
コントリビューター向けのセットアップ手順は [`README.md`](README.md) を参照してください。

**本アプリが管理対象とする基盤技術（重要・必読）:** 本アプリは Microsoft Build 2026 で発表された
ばかりの **WSL Containers**（`wslc` CLI / WSL Container API, Public Preview）をGUIで管理するアプリです。
2026年時点でまだ新しく一般的な資料が少ない技術であるため、設計・実装に着手する前に必ず
[`docs/reference/wsl-containers-platform.md`](docs/reference/wsl-containers-platform.md) を読んでください。
このドキュメントはPreview版APIの仕様サマリと一次情報源へのリンクをまとめた**外部プラットフォームの
参照資料**です（`docs/design/` とは異なり、自分たちの設計ではなく外部の事実を記録する場所。
詳細は [`docs/reference/README.md`](docs/reference/README.md)）。仕様が変わりやすいため、実装前に
Microsoft Learn MCP（`microsoft-docs` plugin。未導入なら下記「既存の再利用可能なskill/agent」参照）や
公式ドキュメントで最新情報を確認してください。

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

**注意:** 実際の `.slnx`/各層の `.csproj` は Issue #3 で作成済みです（詳細は
[`docs/design/architecture-overview.md`](docs/design/architecture-overview.md) を参照）。

ソリューションファイルは、従来の `.sln` ではなく **`.slnx`**（XMLベースの新形式、.NET SDK 9.0.200以上で
`dotnet new sln` により生成）を採用します（[ADR-0006](docs/adr/0006-adopt-slnx-solution-file-format.md)）。

## 開発フロー（必須）

機能追加・仕様のある変更は、必ず以下の6フェーズを順に行います。
オーケストレーション手順の詳細は [`feature-workflow` skill](.github/skills/feature-workflow/SKILL.md) を参照。

1. **機能設計** — 何を作るか、スコープ、ユーザー価値を明確にする
2. **詳細設計** → `rubber-duck` agentでレビュー
3. **テスト設計**（入力・出力・アサーション・エッジケースを確定。実行可能なテストはRedで書く）→ `rubber-duck` agentでレビュー
4. **実装（厳密なTDD）** — Red → Green → Refactor を1振る舞いずつ反復
   ([`tdd-red`](.github/agents/tdd-red.agent.md) →
   [`tdd-green`](.github/agents/tdd-green.agent.md) →
   [`tdd-refactor`](.github/agents/tdd-refactor.agent.md))
5. **テスト** — 全テストスイートを実行し、UIに関わる変更は既存の `winui-ui-testing` skillでE2E
6. **振り返り** → `rubber-duck` agentでレビュー。必要なら ADR / design doc を更新

単純な機械的修正（タイポ、フォーマット、挙動を変えないリネーム等）はこのフローの対象外です。
下記の「モデルルーティング」を参照してください。

### フェーズごとの完了条件 (Definition of Done)

`.github/skills/feature-workflow/SKILL.md` に一覧があります。要約:
設計フェーズはラバーダックの重大な指摘が解消済みであること、TDDフェーズは対象の振る舞いが
Refactor後もGreenを維持していること、振り返りフェーズはADR/design docが実装と一致していること。

## TDD（厳密なRed-Green-Refactor）

[ADR-0019](docs/adr/0019-adopt-gpt-role-routing-and-workflow-contracts.md) により、振る舞いの実装は
必ず「失敗するテストを先に書く」ことから始めます。Redではテスト記述後に、コンパイルに必要な
レビュー済みの最小シグネチャ・固定値スタブだけを例外として許可します。詳細ルールは
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

## 外部プラットフォームの参照資料 (`docs/reference/`)

[`docs/reference/`](docs/reference/README.md) は、`docs/design/` とは異なり
**外部の製品・プラットフォームの仕様**を記録する場所です。

- 現時点では [`wsl-containers-platform.md`](docs/reference/wsl-containers-platform.md)
  （本アプリが管理対象とする WSL Containers / `wslc` の仕様サマリ）を収録。
- Public Preview中の機能を扱うため、各ドキュメントに「最終確認日」を明記し、
  実装前には一次情報源や Microsoft Learn MCP で最新化を確認すること。

## モデルルーティング（GPT主体）

現在の方針は [ADR-0019](docs/adr/0019-adopt-gpt-role-routing-and-workflow-contracts.md) を参照。
モデル名の新しさだけで選ばず、通常作業・軽量作業・独立レビューを分けます。

| 作業の性質 | agent / 既定モデル | 推論強度 |
|---|---|---|
| 機械的な変更の独立バッチ | [`quick-fix`](.github/agents/quick-fix.agent.md) / `gpt-5.6-luna` | medium |
| レビュー済みの具体的なテスト設計をコード化するRed | [`tdd-red`](.github/agents/tdd-red.agent.md) / `gpt-5.6-luna` | medium |
| 通常のGreen | [`tdd-green`](.github/agents/tdd-green.agent.md) / `gpt-5.6-terra` | medium |
| Refactor / ADR本文 | [`tdd-refactor`](.github/agents/tdd-refactor.agent.md)、[`adr-writer`](.github/agents/adr-writer.agent.md) / `gpt-5.6-terra` | medium |
| 機能設計・詳細設計・テスト設計・全体進行 | セッションの推奨モデル `gpt-5.6-terra` | medium |
| 詳細設計・テスト設計・振り返りの独立レビュー | [`rubber-duck`](.github/agents/rubber-duck.agent.md) / `gpt-5.6-sol` | high |
| 難所の実装・判断 | 同じ担当agentを `gpt-5.6-sol` で再実行 | high |
| Solでも解決しない難所・長時間の自律調査 | ユーザー確認後に `gpt-6-astra` | high |

### 選択とエスカレーション

- custom agentには既定の`model`を明示します。優先順位は **ユーザーの明示指定 → 必須の
  エスカレーション → 条件を満たす単純GreenのLuna指定 → agentの既定** です。
  ユーザーの制約がエスカレーションを妨げる場合は確認し、無断で変更しません。
- `task`が対応する場合は`model`と`reasoning_effort`を明示します。推論強度をagentの未対応
  frontmatterで設定したつもりにせず、呼び出し引数で指定します。`context_tier`は通常`default`です。
- 文書やagentのモデル指定は**実行中の親セッションを変更しません**。親の推奨はTerraですが、
  現在のモデルが異なる場合はそのまま明示し、自動で切り替わったとは報告しません。
- GreenをLunaへ下げられるのは、親がレビュー済みの契約・アサーション・実装先を確認し、
  既存パターンの機械的な実装で、新しい設計、外部連携の調整、並行処理、状態遷移を含まない場合だけです。
  その判断を依頼に明記して`model: "gpt-5.6-luna"`を指定します。迷う場合はTerraを維持します。
- 仕様不足・未決定の層配置は設計へ差し戻します。同じ失敗への修正が2回失敗したら反復を止め、
  証拠と未解決点を親へ返し、Luna→Terra→Solの順に担当モデルを上げます。
  実装は同じフェーズ専用agentで行い、読み取り専用レビュアーへ実装を依頼しません。
- 指定モデルが利用不能なら停止し、利用可能なGPT代替をユーザーに確認します。
  非GPTへの自動fallback、無断の下位モデル利用、失敗の成功扱いは禁止です。
- 2回以内の直接tool callで済む機械的変更は親が直接行い、委譲を強制しません。
  より大きい独立バッチのみ`quick-fix`を使います。TDDのフェーズ分離にはこの例外を適用しません。

### 独立レビュー

`rubber-duck`は読み取り・検索・Web参照だけを許可したSolのレビュアーです。
呼び出し前に利用可能なagent一覧の正確な識別子を確認します。追加直後など未登録の場合に限り、
利用可能な`general-purpose`をSolで呼び、編集・shell実行・委譲を禁止する指示を明記します
（指示による制限であり、専用agentと同等のツール制限ではありません）。
代替も利用不能ならレビュー未完了として停止します。存在しない`agent_type`を呼ばず、
自己レビューで代替したり、再帰的にレビュアーを起動したりしません。
依頼には対象・仕様・制約・未決定事項を渡し、指摘の重大度、根拠、修正条件を返してもらいます。

モデルの役割分担は運用上の選択であり、本リポジトリで品質・費用が最適と実証済みという意味ではありません。
見直し時は代表的な作業の完了品質、手戻り回数、所要時間、実際の消費量で比較します。

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

### `microsoft-docs` plugin（`microsoftdocs/mcp`）

- Microsoft Learn の公式ドキュメントを直接検索・取得できるMCPツールを提供する。
- 本プロジェクトが対象とする WSL Containers（`wslc`）は2026年に発表されたばかりの
  Public Preview機能で情報の鮮度が重要なため、実装・設計時は積極的にこれを使って
  一次情報を確認すること（[`docs/reference/wsl-containers-platform.md`](docs/reference/wsl-containers-platform.md) も参照）。

## セットアップ

Copilot CLIでの開発に必要なplugin導入手順は人間の開発環境セットアップに関する内容のため、
[`README.md`](README.md) の「セットアップ」節を参照してください。上記pluginが未導入の環境で
作業していると判明した場合は、その手順をユーザーに案内してください。

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
| Agent | `.github/agents/rubber-duck.agent.md` | Solによる読み取り専用の独立レビュー |
| Skill | `.github/skills/feature-workflow/SKILL.md` | 6フェーズ開発フローのオーケストレーション |
| Skill | `.github/skills/adr-workflow/SKILL.md` | ADR作成・更新の実務手順 |
| Skill | `.github/skills/design-doc-maintenance/SKILL.md` | 設計ドキュメントのスナップショット更新手順 |
| Skill | `.github/skills/store-release-workflow/SKILL.md` | Microsoft Storeリリースのエンドツーエンド手順 |
| Reference | `docs/reference/wsl-containers-platform.md` | WSL Containers(`wslc`)プラットフォームの仕様サマリ |

## スコープ外（引き続き未着手）

- Domain/Application/Infrastructure層の実装（コンテナ/イメージ/ボリューム/ネットワーク管理等の実際のユースケース）
- DIコンテナの導入（Application/Infrastructureが空の間は不要なため見送り中）
- CI/CD（GitHub Actions）ワークフローの新規構築
