# Presentation層: コンテナ一覧ViewModelの状態管理

> このドキュメントは現時点のスナップショットです。経緯・検討過程は書きません。

## 概要

`ContainersViewModel`（`ViewModels/ContainersViewModel.cs`）は、コンテナ一覧の取得・起動・停止・
再起動・削除・詳細表示・ログ表示・対話的シェル表示を担うViewModel。各操作は非同期に実行され、
`await` 中に一覧が差分更新されるケース（ユーザーによる手動更新、他操作完了に伴う
バックグラウンド再同期）を考慮した設計になっている。

## 一覧UI

- `ContainersPage`は標準`ListView`を表形式に並べてコンテナ一覧を表示する。CommunityToolkitの
  `DataGrid`はスクロールバー状態更新タイマー内でUIスレッドCOM例外が発生するため使用しない。
- 表形式の`ListView`は`TableListViewItemStyle`で行コンテンツを横方向いっぱいに広げ、ヘッダーGridと
  行Gridの水平paddingを揃える。
- 一覧列は名前、イメージ、状態、作成日時、操作を表示する。状態列は`DisplayState`
  （`ContainerRowDisplayState`。実際の`State`と進行中の操作種別`PendingOperation`の組み合わせ）を
  表示用テキストへ変換し、実行中/停止中に加えて起動中・停止処理中・再起動中・削除中といった
  途中状態も一覧上で確認できる。
- 行操作は行右端の`…`ボタンに集約し、`Details`、`Shell`、`Logs`、起動・停止・再起動・削除を
  メニューとして表示する。起動・停止・再起動・削除は対象コンテナの状態に応じて実行可能な項目だけを表示する。
- 詳細・ログ・シェルは一覧の下ではなく右側の補助ペインに表示する。複数パネルが開いても一覧領域の高さを
  維持し、補助ペイン側だけを縦スクロールさせる。

## 一覧の差分更新と行インスタンスの維持

- `Containers`（`ObservableCollection<ContainerRowViewModel>`）は `ReplaceContainers` によって
  **差分更新**される。汎用ヘルパー `ObservableCollectionReconciler.Reconcile`（`Collections/`）が、
  キー一致で既存の `ContainerRowViewModel` インスタンスを維持したまま、追加・削除・並び替え・
  インプレース更新のみを適用する（`ObservableCollection.Clear()` を使わないため `ListView` の
  Reset が発生せず、選択やスクロール位置が保たれる）。差分更新を採用した理由と、他の一覧
  （Images/Networks/Volumes）を含む一覧全体での方針は [ADR-0013](../adr/0013-adopt-differential-updates-for-list-views.md) を参照。
- 行キーは `BuildContainerKey`（`Id ∣ Name ∣ Image ∣ CreatedAt`）。`State` はキーに含めず、
  キー一致時に `ApplyFrom` でインプレース更新する。これにより状態遷移（例: Stopped→Running）は
  同一インスタンスのまま反映され、Name/Image/CreatedAt など他の表示フィールドが変わった場合のみ
  該当行が作り直される（外部改名などによる表示の陳腐化を防ぐ）。
- 行インスタンスは基本的に維持されるが、上記のキー変化や、サーバー応答からの消失・再出現に
  よって作り直される場合がある。そのため、非同期操作の開始時にコマンド引数として受け取った
  行インスタンス（`row`）は、`await` の後には既にライブの `Containers` に存在しない可能性がある。
  この行インスタンスは操作対象の `Id` を得る以外の目的で使い続けない。
- `FindLiveRow(string containerId)` が、その時点でのライブな `Containers` からIDで行を
  再検索する唯一の手段。`await` をまたいだ後の状態反映（busy解除、`ApplyFrom` によるプロパティ
  反映、削除時の `Containers.Remove`）は必ずこのメソッドで取得した行に対して行う。

## busy状態と進行中操作種別の永続化

- 行の `IsBusy`・`PendingOperation`（進行中の操作種別。`ContainerRowOperation`の`None`/`Starting`/
  `Stopping`/`Restarting`/`Deleting`のいずれか）は `ContainerRowViewModel` インスタンスに紐づく
  プロパティだが、差分更新で行が作り直されたり、一覧から一時的に消えて再出現したりする場合がある。
- `ContainersViewModel` はコンテナID単位で `_pendingOperations`（`Dictionary<string, ContainerRowOperation>`）
  に進行中操作を保持する。キーが存在すること自体がbusy中を表し、値がその操作種別を表す
  （busy状態と操作種別は必ず一致するため単一の辞書で一元管理する）。`BeginBusy(id, operation)`/
  `EndBusy(id)` で追加・削除する。`ReplaceContainers` は差分更新の際、行を新規生成するときも
  既存行を更新するときも `ApplyPendingOperation` を通じてこの辞書を参照し、該当IDがあれば
  `IsBusy = true` と `PendingOperation` を復元し、なければ解除する。
- `EndBusy` は、対応する `TryRefreshSilentlyAsync`（内部で `ReplaceContainers` を呼ぶ）より
  必ず前に呼び出す。順序を守らないと、再同期時に `_pendingOperations` に残ったままの古い記録を
  見て、実際には完了した操作の途中状態表示を解除できなくなる。
- 行の表示用状態 `DisplayState`（`ContainerRowDisplayState`。`State`と`PendingOperation`の組み合わせ）
  は、`State`・`PendingOperation`いずれの変更でも再計算される。差分更新で行インスタンスが維持される
  ため、`ContainersPage.xaml` の State列は `Text="{x:Bind DisplayState, Mode=OneWay}"` とし
  （`x:Bind` の既定 `OneTime` では途中状態が反映されない）、`BeginBusy` による `Stopping` などの
  途中状態がその場で一覧に反映される。State列のコンバーターは
  `ContainerDisplayStateResourceKeySelector.GetResourceKey(DisplayState)` でリソースキーを選択し
  （`PendingOperation`が`None`以外ならその操作種別を優先、`None`なら`State`から選択）、
  `ResourceLoader` でローカライズ済みテキストへ変換する。

## 削除の楽観的更新とpending管理

- `DeleteAsync` は、Start/Stop/Restartと異なりバックグラウンドでの全件再同期を行わず、
  成功時にその場で対象行を `Containers` から取り除く。
- `DeleteAsync` の実行中（`await` の開始からサーバー応答まで）、対象コンテナIDを
  `_pendingDeleteContainerIds` に記録する。`ReplaceContainers` はこの集合に含まれるIDを
  一覧の再構築から除外するため、削除中に他操作完了に伴うベストエフォート再同期やユーザーの
  手動更新が走っても、サーバーからまだ削除完了前の状態が返り、削除中の行が一覧に再度現れる
  ことを防ぐ。
- `_busyContainerIds` と `_pendingDeleteContainerIds` は役割が異なる別々の集合として管理する。
  前者はボタンの有効/無効制御・途中状態表示、後者は一覧からの除外制御であり、削除操作中は
  両方に同じIDが同時に含まれ得る（前者は実体としては `_pendingOperations` のキー集合）。

## 操作失敗時の復旧

- 各操作（起動・停止・再起動・削除）が例外で失敗した場合は `HandleOperationFailureAsync` に
  処理を委譲する。`EndBusy` でbusy状態を解除し、エラーメッセージを設定したうえで
  `TryRefreshSilentlyAsync` によりベストエフォートで一覧をサーバー側の実際の状態に合わせ直す。
- `TryRefreshSilentlyAsync` は例外を握りつぶすため、再同期に失敗しても直前の楽観的更新
  （またはエラー表示）を維持する。`HandleOperationFailureAsync` はこの再同期の成否を
  `bool` としてそのまま呼び出し元へ返す。

## 削除失敗時の行復元

- `DeleteAsync` は削除中、対象コンテナIDを `_pendingDeleteContainerIds` に記録し、
  `ReplaceContainers` による再構築から除外し続ける（「削除の楽観的更新とpending管理」参照）。
  そのため、削除が例外で失敗し、かつ `HandleOperationFailureAsync` 内の復旧用リフレッシュ
  （`TryRefreshSilentlyAsync`）も失敗した場合、対象の行は一覧から消えたまま復元されない。
- 実際にはサーバー側にまだ存在するコンテナのため、`RestoreRowIfDeleteFailedAndMissing` が
  「復旧用リフレッシュが失敗している」かつ「対象行が一覧に見当たらない（`FindLiveRow` で
  未検出）」の2条件を確認したうえで、削除開始時に受け取った行インスタンスをそのまま
  `Containers` へ戻す。
- 復旧用リフレッシュが成功していれば、その時点のサーバー側の実際の状態が既に一覧へ
  反映されているため、この復元処理は行わない。

## 詳細パネルの状態管理

- `OpenDetailAsync`は`IContainerManagementService.GetContainerDetailAsync`で対象コンテナの詳細を取得し、
  `SelectedContainerDetail`と`DetailLines`へ反映して詳細パネルを開く。
- `DetailLines`はXAML側でそのまま表示できる行単位の文字列であり、ID、名前、イメージ、状態、作成日時、
  コマンド、エントリポイント、ポート、環境変数、マウント、ネットワーク、開始/終了時刻、終了コードを
  現在取得できる範囲で整形する。
- 詳細取得に失敗した場合は詳細パネルを開いたまま`DetailErrorMessage`を設定し、以前の
  `SelectedContainerDetail`と`DetailLines`はクリアする。
- `CloseDetailsAsync`は詳細パネルだけを閉じる。ログやシェルなど他の補助パネルが開いている場合、
  右側の補助ペイン自体は表示したままにする。

## ログパネルの状態管理

- `OpenLogsAsync`は既存の追跡を停止し、`LogLines`、一時停止バッファ、エラー状態、ステータスメッセージを
  初期化してから対象コンテナの既存ログを取得する。
- 既存ログ取得前に対象コンテナの存在を確認する。存在しない、または取得中に
  `ContainerNotFoundException`が発生した場合は、ログパネルを開いたまま「選択中コンテナが存在しない」
  ステータスを表示する。
- 実行中コンテナの場合は既存ログ表示後に`FollowContainerLogsAsync`を開始し、新規ログだけを追跡する。
  停止中コンテナの場合は既存ログのみ表示し、ライブ追跡は開始しない。
- ライブ追跡から到着した行は`IUiDispatcher`経由でUIスレッドへ反映する。実アプリでは
  `DispatcherQueueUiDispatcher`、単体テストでは即時実行または記録用ディスパッチャを使う。
- `PauseLogsAsync`中に到着した行は`_pausedLogBuffer`に保持し、`ResumeLogsAsync`で受信順に
  `LogLines`へ反映する。`ClearLogsAsync`は表示中の行と一時停止バッファを両方クリアするが、
  追跡自体は継続する。
- `CloseLogsAsync`は追跡用`CancellationTokenSource`をキャンセルしてからログパネルを閉じる。

## シェルパネルの状態管理

- `OpenShellAsync`はシェルパネルを開き、対象コンテナの既存セッションが接続中であればそれを再利用する。
  接続中セッションがない場合は`IContainerManagementService.OpenExecSessionAsync`で新しい
  `IContainerExecSession`を開始する。
- シェルセッションはコンテナID単位で`_execSessions`にキャッシュする。閉じたセッションは再利用せず、
  次回オープン時に削除して作り直す。別コンテナのシェルを開く場合は、現在表示中のセッションを閉じてから
  新しい対象へ切り替える。
- 停止中コンテナなどApplication層がexec開始を拒否した場合は、シェルパネルを開いたまま
  `ShellStatusMessage`へ接続不可状態を表示し、`IsShellError`でエラー状態にする。
- `SendShellCommandAsync`は現在のセッションへ入力文字列を送信し、送信後に`ShellCommandText`を空にする。
  送信前に末尾のCR/LFだけを取り除くため、Windows側の入力で`ls\r`のような文字列になっても
  シェルへは`ls`として渡る。空白だけの入力、または未接続状態では送信しない。送信に失敗した場合は
  入力欄の内容を保持し、シェルエラーとして表示する。
- `ReadShellOutputAsync`は`IContainerExecSession.ReadOutputAsync`から出力チャンクを受け取り、
  `IUiDispatcher`経由で`ShellOutput`へ追加する。行単位ではなくチャンク単位で表示するため、
  改行で終わらないプロンプトやコマンド出力もUIへ反映できる。
- 出力ストリームが終了した場合は通常の切断として`IsShellConnected=false`にし、
  `ShellStatusMessage`へ切断状態を表示する。読み取り中の例外はシェルエラーとして表示する。
- `CloseShellAsync`はシェルパネルを閉じる。接続中セッションがある場合はセッションを閉じ、読み取りタスクの
  終了を待ってから接続状態を解除する。停止中コンテナのようにセッションが作成されていないエラー表示状態でも
  パネルを閉じられる。閉じる操作はコマンド入力行の`Close shell`ボタンから実行できる。

## ログ一覧の自動スクロール

- WinUI公式の「[Inverted lists](https://learn.microsoft.com/en-us/windows/apps/design/controls/inverted-lists)」
  パターンに従い、`LstLogs`（ログ表示用`ListView`）の`ItemsPanel`を`ItemsStackPanel`に差し替え、
  `ItemsUpdatingScrollMode="KeepLastItemInView"`を設定する。これにより「末尾を表示している間は
  新しい行が追加されると自動的に末尾までスクロールする」「途中までスクロールして読んでいる間は
  自動スクロールしない」という受け入れ基準をWinUI自身が保証する。
- 自前で`ScrollViewer`を探索・監視し、スクロール位置から末尾判定を行うコード（旧
  `Scrolling/ScrollPositionEvaluator`、`ContainersPage`側の`ViewChanged`購読・`ChangeView`呼び出し）は
  不要となり削除した。`ContainersPage`のコードビハインドはログ表示に関して状態を持たない。
- 検証の過程で、自前実装には次のような未文書化の挙動への依存があったため、公式パターンへの
  置き換えに踏み切った: `ScrollViewer.ChangeView`のverticalOffsetは公式ドキュメント上
  「0から`ScrollableHeight`までの値」が契約であり、範囲外の値（`ExtentHeight`そのものや
  `double.MaxValue`）が常にクランプされる保証はない。また、新規追加行のレイアウトが
  `ExtentHeight`に反映されるタイミングとの競合により、自前実装では追従が途中で解除される
  不具合が発生していた。

## ログ/シェルパネルの個別ウィンドウ表示（ポップアウト）

- 右側の補助ペインは小さいため、ログ・シェルパネルのヘッダーに「Open in window」ボタン
  （`BtnOpenLogsWindow`/`BtnOpenShellWindow`）を追加し、同じ内容を大きな個別ウィンドウ
  （`Windows/LogsWindow.xaml`・`Windows/ShellWindow.xaml`）としても表示できる。小さい補助ペイン自体は
  引き続き表示されたままで、どちらからでも同じ状態（`ContainersViewModel`の`LogLines`/`ShellOutput`
  や各種コマンド）を操作できる。詳細パネルはこの導線の対象外。
- 各ウィンドウは対象ごとに1つだけ開く。ボタンを押したとき既に開いていれば新規に開かず、既存の
  ウィンドウを`Activate()`するだけにとどめる。この生成/再利用/Closed後の再生成ロジックは
  `Windows/SingleInstanceWindowOpener.cs`（`SingleInstanceWindowOpener<TWindow>`）に切り出されている。
  `TWindow`を実際のWinUI `Window`型に固定せず、生成・Activate・Closed購読をすべてデリゲート経由で
  受け取ることで、実ウィンドウを介さずにMSTestで検証できるようにしている。
- `ContainersPage`は`Frame.Navigate`のたびに作り直されるため、ウィンドウ参照をページのフィールドで
  保持することはできない。そこで`Windows/ContainerAuxiliaryWindowManager.cs`をDIの
  Singleton（`App.xaml.cs`の`ConfigureServices`で登録）として用意し、`SingleInstanceWindowOpener<LogsWindow>`と
  `SingleInstanceWindowOpener<ShellWindow>`を1組ずつ合成して保持する。`ContainersPage`のClickハンドラは
  `_windowManager.ShowLogsWindow()`/`ShowShellWindow()`を呼ぶだけで、ウィンドウの生成・再利用判断には
  関与しない。
- `LogsWindow`/`ShellWindow`は小さい補助ペインと同じ`ContainersViewModel`インスタンスを共有する
  （コンストラクタで受け取り、`ViewModel`プロパティとして公開）。そのため以下は既存の設計制約に
  起因する既知の挙動であり、今回のスコープでは対応しない:
  - `ContainersViewModel`は単一コンテナの状態のみを保持するため、ポップアウトを開いたまま
    別コンテナのログ/シェルを開くと、ポップアウト側の表示内容も暗黙に切り替わる。
  - `ShellCommandText`は小さいシェルパネルとポップアウト`ShellWindow`の間でTwoWay共有されるため、
    両方を同時に表示して入力すると干渉し得る。
  - ウィンドウサイズは`Activated`イベント時に取得した`XamlRoot.RasterizationScale`を用いた近似的な
    DPI考慮のみ（1000x700 DIPs相当）で、モニター間移動時のDPI変化への追従までは行わない。
- ポップアウト側にもログ用のPause/Resume/Clear/Close、シェル用のコマンド入力・Send・Closeを
  再配置しており、ユーザーが`ContainersPage`から離れてポップアウトだけが残っている状態でも
  セッションを操作・終了できる。ただし、ウィンドウのタイトルバーの閉じるボタン（×）はウィンドウを
  閉じるだけで、ログ追跡やシェル接続そのものは停止しない（停止するには各ウィンドウ内の
  `CloseLogsCommand`/`CloseShellCommand`を使う）。ウィンドウを閉じる操作とセッションを終了する操作は
  意図的に分離している。
