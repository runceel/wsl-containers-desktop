---
description: 'MSTestによる単体テストの規約とTDDでの許可/禁止事項'
applyTo: '**/*Tests.cs'
---

# テストコード規約 (MSTest)

単体テストフレームワークは MSTest を採用している（[ADR-0003](../../docs/adr/0003-select-mstest-as-unit-test-framework.md)）。
テストは厳密なTDD（[ADR-0019](../../docs/adr/0019-adopt-gpt-role-routing-and-workflow-contracts.md)）の一部として書く。

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
| Red | 失敗するテストの追加・修正。テスト記述後にコンパイル用のレビュー済み最小シグネチャ・固定値スタブのみ例外として追加可 | 振る舞いの実装、未決定の設計、既存成功テストの期待値変更 |
| Green | 直前の失敗テストを通す最小実装 | テストの追加・変更、関係ない機能追加 |
| Refactor | プロダクションコード・テストコード双方の構造改善（挙動不変） | 新しい振る舞いの追加、テストの期待値変更 |

- Red フェーズで書くテストは、ラバーダックでレビュー済みの仕様・詳細設計を反映する。
  フェーズ3「テスト設計」では入出力・アサーション値・エッジケースを確定し、
  実行可能なMSTestテストはフェーズ4のRedで書く。
- Redのスタブは、空のvoid、固定値/default、非同期の完了済み結果だけを許可する。
  必要なシグネチャと戻り値はテスト設計でレビュー済みとし、分岐・I/O・ドメインロジック・
  例外を投げるスタブは禁止する。コンパイルや環境のエラーではなく、意図したアサーション失敗を確認する。
- Green フェーズでは「テストを通す」以上の実装をしない（過剰実装をしない）。Green を担当する
  `tdd-green`はTerraを既定とし、親が単純Green条件を確認した場合だけLunaを指定する。
  仕様や設計が足りなければ設計へ戻す。モデル選択・エスカレーションは
  [`AGENTS.md`](../../AGENTS.md)のモデルルーティングに従う。
- Refactorでは各変更のたびに影響範囲のテストを再実行し、Greenを維持する。
  全スイートはフェーズ5で実行する。未実行範囲や失敗を成功扱いにしない。

## モック・スタブ

- Application層のユースケーステストでは、Application層で定義された外部依存の抽象（インターフェース）をテストダブルに
  差し替える。Infrastructureの実クライアントに依存するテストは書かない。
- モックライブラリを追加する場合は、選定をADRとして記録する
  （テストダブルの方針自体がプロジェクトの重要な決定であるため）。

## 命名規則

- テストプロジェクト: `<対象プロジェクト名>.Tests`
- テストクラス: `<対象クラス名>Tests`
- テストファイル: `applyTo` の対象となるよう `*Tests.cs` で終える。
