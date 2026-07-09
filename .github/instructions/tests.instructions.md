---
description: 'MSTestによる単体テストの規約とTDDでの許可/禁止事項'
applyTo: '**/*Tests.cs'
---

# テストコード規約 (MSTest)

単体テストフレームワークは MSTest を採用している（[ADR-0003](../../docs/adr/0003-select-mstest-as-unit-test-framework.md)）。
テストは厳密なTDD（[ADR-0002](../../docs/adr/0002-adopt-strict-tdd-workflow.md)）の一部として書く。

## 構造: Arrange-Act-Assert (AAA)

```csharp
[TestMethod]
public async Task StartAsync_ContainerIsStopped_TransitionsToRunning()
{
    // Arrange
    var container = ContainerBuilder.Stopped().Build();
    var sut = new ContainerLifecycleService(container);

    // Act
    await sut.StartAsync();

    // Assert
    Assert.AreEqual(ContainerState.Running, container.State);
}
```

- 1テスト1アサーション対象（1つの振る舞い）を原則とする。関連する複数のアサーションは許容するが、
  無関係な検証を1つのテストメソッドに詰め込まない。
- テストメソッド名は `対象メソッド_条件_期待結果` の形式にする。

## TDDフェーズごとの許可/禁止事項

`.github/agents/tdd-red.agent.md` / `tdd-green.agent.md` / `tdd-refactor.agent.md` に対応する。

| フェーズ | このフェーズで許可されること | 禁止されること |
|---|---|---|
| Red | 失敗するテストの追加・修正 | プロダクションコードの変更 |
| Green | 直前の失敗テストを通す最小実装 | テストの追加・変更、関係ない機能追加 |
| Refactor | プロダクションコード・テストコード双方の構造改善（挙動不変） | 新しい振る舞いの追加、テストの期待値変更 |

- Red フェーズで書くテストは、事前にラバーダック（`rubber-duck` agent）でレビュー済みの
  仕様・詳細設計を反映したものにする。「テスト作成」フェーズのレビューでは、具体的な
  入出力・アサーション値・エッジケースまで確定させておくこと
  （[ADR-0008](../../docs/adr/0008-expand-model-routing-to-mechanical-workflow-steps.md)
  により、`tdd-red` agentは既定でコスト最適化モデルを使うため、ここが曖昧なまま
  Redフェーズに進まないこと）。
- Green フェーズでは「テストを通す」以上の実装をしない（過剰実装をしない）。Green を担当する
  `tdd-green` agent は既定でコスト最適化モデル（`mai-code-1-flash-picker`）を使う
  （[ADR-0016](../../docs/adr/0016-set-sonnet-5-baseline-and-route-green-to-flash.md)）。ただし
  コンパイル/実装のために新しい非自明な層配置・設計判断が必要になった場合は、ベースラインモデルへ
  エスカレーションする。
- Refactor フェーズでは各変更のたびにテストを再実行し、常にGreenを維持する。

## モック・スタブ

- Application層のユースケーステストでは、Infrastructure層の抽象（インターフェース）をテストダブルに
  差し替える。Infrastructureの実クライアントに依存するテストは書かない。
- モックライブラリを追加する場合は、選定をADRとして記録する
  （テストダブルの方針自体がプロジェクトの重要な決定であるため）。

## 命名規則

- テストプロジェクト: `<対象プロジェクト名>.Tests`
- テストクラス: `<対象クラス名>Tests`
- テストファイル: `applyTo` の対象となるよう `*Tests.cs` で終える。
