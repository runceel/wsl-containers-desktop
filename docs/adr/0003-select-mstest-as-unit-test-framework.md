# 0003. 単体テストフレームワークとしてMSTestを採用する

## Status

Accepted

## Context

- ADR-0002 で厳密なTDDサイクルを採用することを決めた。TDDを実践するには
  単体テストフレームワークを一つに定める必要がある。
- 候補として MSTest / xUnit / NUnit を検討した。
  - xUnit は .NETコミュニティで広く使われているが、
  - .NETチーム公式の Copilot skill 群（`dotnet/skills` marketplaceの `dotnet-test`,
    `dotnet-test-migration` プラグイン）は MSTest ワークフロー（Microsoft.Testing.Platform
    への移行、MSTestバージョンアップ等）を重視しており、Copilotエージェントによる
    テスト生成・実行・移行の支援が最も手厚い。
  - NUnitは同等の公式Copilot skill支援が薄い。
- 本プロジェクトはCopilotエージェントによる開発比率が高いため、
  エージェント支援の手厚さを優先する。

## Decision

- 本プロジェクトの単体テストフレームワークとして **MSTest** を採用する。
- 新規に作成するテストプロジェクトはすべてMSTestベースとする。
- テストコードの規約は `.github/instructions/tests.instructions.md` に従う。

## Consequences

- `dotnet/skills` marketplaceの `dotnet-test` / `dotnet-msbuild` プラグインによる
  テスト実行・生成・カバレッジ支援をそのまま活用できる。
- 将来的にテスト実行基盤を Microsoft.Testing.Platform に移行する場合も、
  `dotnet-test-migration` プラグインの支援を受けられる。
- xUnit/NUnitに慣れたコントリビューターには多少の学習コストが発生するが、
  アサーション構文の差異は軽微であり許容する。
