---
description: 'XAML / WinUI 3 のマークアップ規約'
applyTo: '**/*.xaml'
---

# XAML / WinUI 3 規約

既存の `winui-design` skill（`winui` plugin, 導入済み）の指針と整合させること。
ビルド・実行手順は `winui-dev-workflow` skill、UI自動テストは `winui-ui-testing` skill を利用する
（本プロジェクトでは重複して定義しない）。

## バインディング

- 可能な限り `x:Bind` を使う（`Binding` はコンパイル時チェックが効かないため避ける）。
- TextBoxなど即時反映が必要な双方向バインディングには
  `Mode=TwoWay, UpdateSourceTrigger=PropertyChanged` を指定する
  （既定の `LostFocus` トリガーだとUIオートメーションテストの `set-value` が反映されない問題を避ける）。
- 表示のみのバインディングは既定（`OneTime`）ではなく `Mode=OneWay` を明示する
  （`x:Bind` の既定値 `OneTime` により値が更新されない不具合を避けるため）。

## AutomationId

- ユーザーが操作するすべてのインタラクティブ要素（Button, TextBox, ComboBox, ToggleSwitch等）に
  `AutomationProperties.AutomationId` を設定する。
- `winui-ui-testing` skillのアクセシビリティ監査は、AutomationId未設定の要素を検出対象とするため、
  新しいコントロールを追加したら必ず設定すること。

## 命名

- コントロール名（`x:Name`）は種別プレフィックスを付ける（例: `BtnStart`, `TxtContainerName`,
  `CmbRuntime`, `TglAutoStart`, `LstContainers`）。UIテストのAutomationIdもこれに合わせる。

## レイアウト

- ウィンドウサイズ・余白は `winui-design` skillのチェックリスト
  （スクロールバー不要、要素の見切れ・重なりがない、余白が意図通り）に従う。
