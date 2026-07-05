# UI / ビジュアルデザイン方針

> このドキュメントは現時点のスナップショットです。経緯・検討過程は書きません。

## 目的

WSL Containers Desktop は、WSL Containers を管理する Windows ネイティブの開発者向けデスクトップアプリである。
UIは、コンテナの状態・操作・ログを短時間で把握できるよう、Fluent Design に沿った明確な視覚階層を持つ。

## 基本原則

- Windows 11 / WinUI 3 の標準コントロールと Fluent Design の見た目を優先する。
- Mica の背景を活かし、画面全体を単色の暗い面で覆わない。
- 情報のまとまりはカード面、境界線、余白、アクセントで区切る。
- 独自色は WSL / terminal らしいグリーン系アクセントに限定し、本文や大面積背景には使いすぎない。
- High Contrast では独自色ではなく Windows のシステム色を使う。

## 画面構成

`MainWindow` は `TitleBar`、`NavigationView`、`Frame` で構成する。

- トップレベルナビゲーションは `NavigationView` の左ペインを使う。
- ウィンドウ背景は `MicaBackdrop` を使う。
- 各ページは `Frame` 内で表示し、ページ本文には `24,16,24,16` の余白を取る。
- ページ内の主要領域は、Mica の背景を透過させる `LayerFillColorDefaultBrush`（塗り）と `CardStrokeColorDefaultBrush`（境界線）を使った面として表現する。

## アプリ独自アクセント

アプリ独自アクセントは `src/WslContainersDesktop.App/Themes/AppThemeResources.xaml` に集約する。

| リソース | 用途 |
|---|---|
| `WslContainersAccentFillColorDefaultBrush` | 見出し横のアクセントバーなど、主要な強調面 |
| `WslContainersAccentFillColorSubtleBrush` | 控えめなアクセント面。現在は将来の補助アクセント用に予約 |
| `WslContainersAccentStrokeColorBrush` | 強調したい境界線・アイコン |
| `WslContainersAccentTextFillColorBrush` | アクセント色のテキスト。現在は将来のテキスト強調用に予約 |
| `WslContainersSurfaceBorderThickness` | カード面の境界線太さ。High Contrastでは通常より太くする |

テーマ辞書は `Default`、`Light`、`HighContrast` を定義する。Darkテーマ用に `Dark` キーは使わない。
XAMLの使用箇所では `{ThemeResource ...}` を使い、テーマ切り替えに追従させる。

## レイアウトと余白

- 余白・間隔・サイズは4pxグリッドに揃える。
- ページ内の主要セクション間は16pxまたは24pxを基準にする。
- カード内部の余白は16pxを基準にする。
- 角丸は標準リソースを使う。カード面は `OverlayCornerRadius`、小さなアクセントバーや操作部品は `ControlCornerRadius` を使う。
- `Grid` を構造化レイアウトに使い、単純な縦横並びだけ `StackPanel` を使う。

## タイポグラフィ

- ページタイトルは `TitleTextBlockStyle` を使う。
- セクション見出しは `SubtitleTextBlockStyle` を使う。
- 通常説明文は既定の本文スタイルを使い、不要な `FontSize` や `FontWeight` は指定しない。
- 強調が必要な本文のみ `BodyStrongTextBlockStyle` を使う。

## コンテナ一覧画面

`ContainersPage` は以下の面で構成する。

| 領域 | 方針 |
|---|---|
| ヘッダー | カード面にページタイトル、説明、更新ボタンを置き、左端にグリーン系アクセントバーを表示する |
| エラー表示 | `InfoBar` をヘッダー直下にインライン表示する |
| コンテナ一覧 | 標準 `ListView` をカード面で囲み、表の境界を背景から分離する |
| 空状態 | 中央にカード面を置き、一覧が空であることを背景と区別して表示する |
| ログパネル | 下部カード面として表示し、アクセント色の境界線で一覧領域と区別する |

行操作は行右端の `…` メニューに集約する。
ログ一覧は `ListView` と `ItemsStackPanel.ItemsUpdatingScrollMode="KeepLastItemInView"` を使い、スクロール挙動をWinUI標準に委譲する。

## 設定画面

`SettingsPage` は、設定項目が少ない状態でも空白だけに見えないよう、ページ説明をカード面に載せる。
今後設定項目を追加する場合も、関連する設定をカード単位でまとめる。

## 色・素材

- 背景は Mica を基本とし、ページ本文のまとまりは Mica を透過させる `LayerFillColorDefaultBrush` の面で表す。
- XAML使用箇所で色コードを直接指定しない。
- 通常テーマの独自アクセント色は `AppThemeResources.xaml` のみに置く。
- High Contrast のテーマ辞書では `SystemColor*Brush` のみを使い、独自の緑色や不透明度で上書きしない。

## アイコン

- 標準的な操作・ナビゲーションには Segoe Fluent Icons / `FontIcon` / `SymbolIcon` を使う。
- ナビゲーションアイコンは `NavigationViewItem` の標準状態色に従わせ、選択状態やHigh Contrastでのコントラストを維持する。
- 画像アイコンはアプリロゴなどブランド資産に限定する。

## フィードバックと状態表示

- 操作失敗やログ取得失敗は `InfoBar` でインライン表示する。
- 取り消しにくい操作は `ContentDialog` で確認する。
- コンテナ行の起動中・停止中・再起動中・削除中などの途中状態はViewModelの表示状態に従い、一覧上で確認できるようにする。
- 入力欄に隣接する操作（イメージの pull、ボリューム／ネットワークの作成など）の進行中は、操作ボタンの横に `ProgressRing` を表示し、ボタンと下端を揃える。

## アクセシビリティとローカライズ

- ユーザーが操作するコントロールには `AutomationProperties.AutomationId` を設定する。
- UI文字列は `.resw` と `x:Uid` を使い、XAMLやViewModelに直接埋め込まない。
- アイコンだけの操作にはアクセシブル名を設定する。
- High Contrast では独自色を使わず、システム色で十分なコントラストを確保する。

## レビュー観点

UI変更時は以下を確認する。

- 画面全体が黒一色・白一色に見えず、面の境界がわかる。
- 独自グリーンが強調用途に留まり、本文の読みやすさを妨げていない。
- Light / Default / HighContrast の各テーマ辞書に同じリソースキーが存在する。
- XAML使用箇所は `{ThemeResource}` でテーマリソースを参照している。
- 余白・角丸・境界線が4pxグリッドと標準リソースに沿っている。
- 既存の `AutomationId`、`x:Uid`、ViewModelへのバインディングを壊していない。
