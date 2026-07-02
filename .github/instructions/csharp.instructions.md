---
description: 'C# コーディング規約とクリーンアーキテクチャの層間依存ルール'
applyTo: '**/*.cs'
---

# C# コーディング規約

## 基本方針

- `Nullable` を有効にする。`string?` 等のnull許容を明示し、`!`（null forgiving演算子）の濫用を避ける。
- 非同期メソッドは `Async` サフィックスを付け、`Task`/`Task<T>` を返す。UIスレッドをブロックする
  同期待機（`.Result`, `.Wait()`）は使わない。
- `var` は右辺から型が自明な場合のみ使う。
- 例外は「本当に例外的な状況」でのみ使い、通常の制御フローに使わない。
- パブリックAPI（クラス・メソッド）にはXMLドキュメントコメントを付ける。

## クリーンアーキテクチャの層間依存ルール（[ADR-0005](../../docs/adr/0005-adopt-clean-architecture-layering.md)）

プロジェクトは Domain / Application / Infrastructure / Presentation の4層に分割する方針。
詳細は [`docs/design/architecture-overview.md`](../../docs/design/architecture-overview.md) を参照。

コードを書く・変更する際は、必ず以下を守ること。

| 層 | 依存してよいもの | 依存してはいけないもの |
|---|---|---|
| Domain | なし | Application, Infrastructure, Presentation, WinUI/フレームワーク全般 |
| Application | Domain | Infrastructure, Presentation, WinUI |
| Infrastructure | Application, Domain | Presentation |
| Presentation (View/ViewModel) | Application, Domain | Infrastructureの具象クラスを直接 `new` すること（DI経由の抽象参照はOK） |

- 新しいクラスを追加するときは、まずどの層に属するかを判断してから配置する。
- Application層に置くべきインターフェース（例: `IContainerRuntimeClient`）を
  Infrastructure層やPresentation層に定義しない。
- ViewModelからInfrastructureの具象型を直接参照するコードを見つけたら、それは設計違反。
  Application層の抽象を経由するよう修正する。

## MVVM (Presentation層)

- ViewModelは `CommunityToolkit.Mvvm` の `ObservableObject` / `[ObservableProperty]` /
  `[RelayCommand]` を使う。
- ViewModelはApplication層のユースケース/抽象のみに依存する。WinUIの型（`FrameworkElement` 等）を
  ViewModelのプロパティ・引数に持たせない。
- View（コードビハインド）はUI操作のみを担当し、ビジネスロジックを書かない。

## 命名規則

- クラス・メソッド・プロパティ: `PascalCase`
- ローカル変数・パラメーター: `camelCase`
- プライベートフィールド: `_camelCase`
- インターフェース: `I` プレフィックス（例: `IContainerRuntimeClient`）
