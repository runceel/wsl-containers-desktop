# 0007. WslContainersDesktop.App で WindowsAppSdkDeploymentManagerInitialize を無効化する

## Status

Accepted

## Context

- Issue #3（土台: 空のWinUIウィンドウ起動）でプロジェクトを初めてスキャフォールドした際、
  `WslContainersDesktop.App`（WinUI3 MSIXパッケージアプリ）に対するMSTestベースの単体テスト
  （`WslContainersDesktop.App.Tests`）を実行すると、テストホスト（`testhost.exe`）上で
  `COMException (REGDB_E_CLASSNOTREG)` が発生し、テストが実行できなかった。
- 原因は、Windows App SDKがMSIX/WinExeプロジェクトに対して既定で注入する
  「Deployment Manager自動初期化コード」（`WindowsAppSdkDeploymentManagerInitialize`プロパティで
  制御される）が、パッケージIDを持たないプロセス（vstestの`testhost.exe`）上で実行されると
  失敗するため。
- 当初の回避策として、`WslContainersDesktop.App.Tests.csproj`の`ProjectReference`に対し
  `AdditionalProperties`で`WindowsAppSdkDeploymentManagerInitialize=false`を
  **App参照時のみ**上書きする方法を採った。
- しかし、この方法は`WslContainersDesktop.slnx`のソリューション全体ビルド時に、MSBuildが
  「App単体としてのプロジェクトインスタンス（既定のプロパティ）」と
  「App.Tests経由で参照される、異なるグローバルプロパティを持つApp（テスト用の別インスタンス）」の
  **2つの異なるビルドグラフノード**として同じAppプロジェクトを二重にビルドしてしまうことが判明した。
  この二重ビルドが既定の並列ビルド（`dotnet build`のデフォルト`-m`挙動）下で競合し、
  MSIXパッケージング処理（ペイロード計算ターゲット）が`obj\`と`bin\`双方の
  `WslContainersDesktop.App.dll`/`.pdb`を同一出力先への別々の入力として検出し、
  `APPX1101`（同一パスへの重複書き込み）でビルドが**非決定的に**（並列度・タイミング依存で）
  失敗することがあった（`-m:1`の単一スレッドビルドでは常に成功することを確認済み）。
- Microsoft Learn ドキュメント「Project properties and auto-initializers」を確認したところ、
  Deployment Manager自動初期化は、Main/Singletonパッケージ機能（プッシュ通知など）を使う
  フレームワーク依存パッケージアプリでのみ必要な仕組みであることが分かった。
  本プロジェクトは現時点でそうした機能を一切使用していない。

## Decision

- `WslContainersDesktop.App.csproj`自体の既定値として、`WindowsAppSdkDeploymentManagerInitialize`を
  **無条件に`false`**に設定する。
- これに伴い、`WslContainersDesktop.App.Tests.csproj`の`ProjectReference`に付けていた
  `AdditionalProperties`による同プロパティの上書きは撤去する
  （Appプロジェクト自身の既定値と一致するため、二重ビルドの原因が解消される）。

## Consequences

- テストホスト上での`COMException`が解消され、`WslContainersDesktop.App.Tests`が
  正常に実行できるようになる。
- ソリューション全体（`.slnx`）のビルドにおいて、Appプロジェクトが単一のグローバルプロパティ
  セットでのみビルドされるようになり、`APPX1101`の重複ペイロード競合が解消される
  （クリーンビルドを複数回実行し再現しないことを確認済み）。
- 本アプリが将来、プッシュ通知等のMain/Singletonパッケージ機能を必要とする場合は、
  本ADRを踏まえて以下のいずれかを再検討する必要がある。
  - アプリ自身のコードで明示的に`Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap`
    等のAPIを介して初期化処理を呼び出す。
  - `WindowsAppSdkDeploymentManagerInitialize`を再度有効化した上で、
    テストプロジェクトからのApp二重ビルド問題を別の方法（例:
    テスト対象コードを別アセンブリに切り出す、`Directory.Build.props`でのプロパティ制御の
    見直し等）で解決する。
