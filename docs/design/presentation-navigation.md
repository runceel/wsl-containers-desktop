# Presentation層: ナビゲーション基盤とローカライズ

> このドキュメントは現時点のスナップショットです。経緯・検討過程は書きません。
> 採用理由は [ADR-0007](../adr/0007-disable-windows-app-sdk-deployment-manager-auto-initialize.md) を参照してください。

## 概要

`WslContainersDesktop.App`（Presentation層）は、`NavigationView` + `Frame` による
トップレベルのページ切り替えを、型安全なキー（`NavigationPageKey`）を単一の情報源として実装している。
UI文字列はすべて `.resw` リソースに外出しされ、コードやXAMLに直接ハードコードしない。

## ナビゲーション

### 構成要素

| 型 | 場所 | 役割 |
|---|---|---|
| `NavigationPageKey` | `Navigation/NavigationPageKey.cs` | ページを表す `enum`（`Containers`, `Settings`）。string/Tagベースの遷移は使わない。 |
| `NavigationPageKeyExtensions.EnsureDefined` | `Navigation/NavigationPageKeyExtensions.cs` | `NavigationPageKey` が定義済みの値であることを検証する拡張メソッド。未定義値なら `ArgumentOutOfRangeException` を送出する。`NavigationPageRegistry`・`NavigationViewModel` の双方から共通利用する。 |
| `NavigationPageRegistry` | `Navigation/NavigationPageRegistry.cs` | `NavigationPageKey → Page派生型` のマッピングを一元管理する、XAML非依存の素のC#クラス。 |
| `NavigationViewModel` | `ViewModels/NavigationViewModel.cs` | 現在表示中のページキー（`CurrentPageKey`、setterはprivate）を保持するObservableObject。`NavigateToCommand`でのみ変更できる。 |
| `MainWindow` | `MainWindow.xaml` / `.xaml.cs` | `NavigationView`・`Frame`と上記の型を結びつけるShell。 |

### 状態同期の設計

`NavigationViewModel.CurrentPageKey` を唯一の情報源（single source of truth）とする。

- `MainWindow`のコードビハインドは、XAMLの`x:Name`参照から
  `Dictionary<NavigationViewItem, NavigationPageKey>`を構築する（コンパイル時に検証される。
  `Tag`文字列 + `Enum.Parse`は使わない）。
- `NavigationView.SelectionChanged`は、選択された項目をDictionaryで引いて
  `ViewModel.NavigateToCommand.Execute(pageKey)`を呼ぶだけに限定する。
- `NavigationViewModel.PropertyChanged`（`CurrentPageKey`変更時）を購読する
  `SyncNavigationViewWithCurrentPage()`という**1つの共通メソッド**が、
  `NavigationView.SelectedItem`の同期と`NavigationPageRegistry`経由の`Frame.Navigate`の
  両方を行う。`MainWindow`のコンストラクタからも同じメソッドを1回呼び、初期表示を成立させる。
- トップレベルのページ切り替えはタブ切り替えに相当し、Backナビゲーションの概念を持たない。
  `TitleBar`のBackボタンは無効化しており（`IsBackButtonVisible="False"`）、
  `SyncNavigationViewWithCurrentPage()`は遷移のたびに`NavFrame.BackStack`を明示的にクリアする。

### DIコンテナ

Composition Rootは`App.xaml.cs`に置く。`Microsoft.Extensions.DependencyInjection`で
`IContainerRuntimeClient`→`WslcCliContainerRuntimeClient`、
`IContainerManagementService`→`ContainerManagementService`、`IWslcCliRunner`→`WslcCliRunner`、
`IUiDispatcher`→`DispatcherQueueUiDispatcher`を登録する。

`ContainersPage`は`Frame.Navigate(Type)`から生成されるためパラメーターレスコンストラクタを持つ。
ページは`((App)Application.Current).Services`から`ContainersViewModel`を解決し、ViewModel自体は
Application層の`IContainerManagementService`とUIディスパッチ抽象`IUiDispatcher`だけに依存する。
詳細は [ADR-0010](../adr/0010-adopt-di-container-for-presentation.md) を参照。

## ローカライズ

- 既定（ニュートラル）言語は英語。文字列は `Strings/en-US/Resources.resw` に集約する。
- XAML側は `x:Uid` で文字列を参照する（`NavigationViewItem`の表示名、ページタイトル・説明文、
  `TitleBar`のタイトル等）。ViewModelは表示文字列を一切保持しない。
- パッケージマニフェスト（`Package.appxmanifest`）の`DisplayName`/`Description`も
  `ms-resource:AppDisplayName` / `ms-resource:AppDescription` でローカライズする
  （`Resources.resw`の`AppDisplayName`/`AppDescription`キーを参照）。
- 例外: `Window.Title`は`DependencyProperty`ではないため`x:Uid`によるローカライズ対象にできない。
  `MainWindow`のコンストラクタで`new ResourceLoader().GetString("MainWindow.Title")`を用いて
  明示的に設定する。
- 将来、他言語（例: `ja-JP`）を追加する場合は `Strings/<culture>/Resources.resw` を
  追加するだけでよい構成になっている。
