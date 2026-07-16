# Presentation層: コンテナ管理ViewModelの状態管理

> このドキュメントは現時点のスナップショットです。経緯・検討過程は書きません。

## 概要

`ContainersViewModel`（`ViewModels/ContainersViewModel.cs`）は、`ContainersPage`・`LogsWindow`・
`ShellWindow`が共有するDIシングルトンの公開XAML/コマンドファサード
（[ADR-0017](../adr/0017-split-containersviewmodel-and-runtime-client-into-focused-components.md)）。
ファサードは次の4コンポーネントを所有する。各コンポーネントは個別にはDI登録せず、
`ContainersViewModel`のコンストラクタで同じ`IContainerManagementService`と必要なUI抽象を渡して生成する。

| ファサードプロパティ | コンポーネント | 所有する状態・ライフタイム |
|---|---|---|
| `List` | `ContainerListViewModel` | 一覧取得、差分更新、起動・停止・再起動・削除、行のbusy状態 |
| `Details` | `ContainerDetailsViewModel` | 詳細取得、表示行の整形、詳細エラー、詳細パネル表示 |
| `Logs` | `ContainerLogsViewModel` | ログスナップショット、ライブ追跡、一時停止バッファ、ログパネル表示 |
| `Shell` | `ContainerShellViewModel` | execセッション、入出力、接続状態、シェルパネル表示 |

- `ContainersViewModel`の公開コレクション・状態プロパティは、対応するコンポーネントへの手書きの
  委譲プロパティである。`Containers`、`DetailLines`、`LogLines`、`ShellOutput`は子が保持する
  コレクションインスタンスそのものを返す。`ShellCommandText`は`Shell`へ読み書きの両方を委譲する。
- `RelayPropertyChanges`は各子の`PropertyChanging`/`PropertyChanged`をファサード上の同名プロパティ
  変更として再発行する。`IsDetailPanelVisible`、`IsLogPanelVisible`、`IsShellPanelVisible`の変更時は、
  算出プロパティ`IsSidePanelVisible`の変更も再発行する。これにより、`ContainersPage`・`LogsWindow`・
  `ShellWindow`は既存のファサードプロパティ名を継続してバインドできる。
- `RelayCommand`由来の公開コマンド名はファサードに維持し、各メソッドは対応する子コンポーネントの
  公開メソッドへ委譲する。XAMLと`DashboardViewModel`は子コンポーネントを直接参照しない。

## 一覧UI

- `ContainersPage`は標準`ListView`を表形式に並べてコンテナ一覧を表示する。CommunityToolkitの
  `DataGrid`はスクロールバー状態更新タイマー内でUIスレッドCOM例外が発生するため使用しない。
- 表形式の`ListView`は`TableListViewItemStyle`で行コンテンツを横方向いっぱいに広げ、ヘッダーGridと
  行Gridの水平paddingを揃える。メタデータ領域と幅`96`の操作領域をヘッダー・行で共通化し、
  Application resourcesの`ContainersTableColumnLayout`によって名前・イメージ・状態・作成日時の
  列幅を同期する。変更した幅はページ遷移後も同じプロセス内で維持する。
- ヘッダーの`TableColumnSplitter`で4列をリサイズする。境界を動かすと右側全列の余力を近い順に
  再配分し、要求量が制約を超える場合は可動限界まで移動する。星幅の名前列はリサイズ後も星幅を維持する。
  共通の表レイアウト規則は[UI / ビジュアルデザイン方針](design-policy.md#表形式一覧)、
  採用判断は[ADR-0018](../adr/0018-adopt-resizable-table-layouts-with-fixed-action-rails.md)を参照。
- 一覧列は名前、イメージ、状態、作成日時、操作を表示する。状態列は`DisplayState`
  （`ContainerRowDisplayState`。実際の`State`と進行中の操作種別`PendingOperation`の組み合わせ）を
  表示用テキストへ変換し、実行中/停止中に加えて起動中・停止処理中・再起動中・削除中といった
  途中状態も一覧上で確認できる。
- 行右端の固定操作領域には、対象コンテナの状態に対応する起動または停止のアイコンボタンと`…`ボタンを
  同じ位置に表示する。停止中と起動中はPlay、実行中と停止処理中・再起動処理中はStopを使い、
  処理中は対応するアイコンを無効状態で維持する。Play、Stop、`…`にはローカライズ済みのツールチップと
  アクセシブル名を設定する。起動・停止の表示と実行可否は行ViewModelの各プロパティに従う。`…`メニューには
  `Details`、`Shell`、`Logs`、再起動、削除を表示し、再起動・削除は対象コンテナの状態に応じて
  実行可能な項目だけを表示する。
- 詳細・ログ・シェルは一覧の下ではなく右側の補助ペインに表示する。複数パネルが開いても一覧領域の高さを
  維持し、補助ペイン側だけを縦スクロールさせる。

## 一覧の差分更新と行インスタンスの維持

一覧の状態・ロジックは`ContainersViewModel`ではなく構成要素`ContainerListViewModel`
（`ContainersViewModel.List`）が保持する。以下の`ReplaceContainers`等はすべて
`ContainerListViewModel`のメンバーであり、`ContainersViewModel`は`Containers`/`ErrorMessage`/
`IsEmpty`を転送し、`RefreshCommand`等から対応する`List`の公開メソッドへ委譲するのみ
（「概要」参照）。

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
- `ContainerListViewModel` はコンテナID単位で `_pendingOperations`（`Dictionary<string, ContainerRowOperation>`）
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
- `_pendingOperations` と `_pendingDeleteContainerIds` は役割が異なる別々の状態として管理する。
  前者はボタンの有効/無効制御・途中状態表示（キー集合がbusy中のIDを表す）、後者は一覧からの
  除外制御であり、削除操作中は両方に同じIDが同時に含まれ得る。

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

- 詳細の状態・ロジックは`ContainerDetailsViewModel`（`ContainersViewModel.Details`）が保持する。
  ファサードの`OpenDetailsCommand`/`CloseDetailsCommand`は`Details.OpenAsync`/`CloseAsync`へ委譲する。
- `OpenAsync`は詳細パネルを開いて以前のエラーと表示行をクリアしてから、
  `IContainerManagementService.GetContainerDetailAsync`で対象コンテナの詳細を取得し、
  `SelectedContainerDetail`と`DetailLines`へ反映する。
- `DetailLines`はXAML側でそのまま表示できる行単位の文字列であり、ID、名前、イメージ、状態、作成日時、
  コマンド、エントリポイント、ポート、環境変数、マウント、ネットワーク、開始/終了時刻、終了コードを
  現在取得できる範囲で整形する。
- 詳細取得に失敗した場合は詳細パネルを開いたまま`DetailErrorMessage`を設定する。
- `CloseAsync`は詳細パネルだけを閉じる。ログやシェルなど他の補助パネルが開いている場合、
  右側の補助ペイン自体は表示したままにする。

## ログパネルの状態管理

- ログの状態・ロジックは`ContainerLogsViewModel`（`ContainersViewModel.Logs`）が保持する。
  ファサードのログ関連コマンドは`Logs.OpenAsync`/`PauseAsync`/`ResumeAsync`/`ClearAsync`/`CloseAsync`
  へ委譲する。
- `OpenAsync`は既存の追跡を停止し、`LogLines`、一時停止バッファ、ディスパッチ待ちキュー、
  エラー状態、ステータスメッセージを初期化してから対象コンテナの既存ログを取得する。
- 既存ログ取得前に対象コンテナの存在を確認する。存在しない、または取得中に
  `ContainerNotFoundException`が発生した場合は、ログパネルを開いたまま「選択中コンテナが存在しない」
  ステータスを表示する。
- 実行中コンテナの場合は既存ログ表示後に`FollowContainerLogsAsync`を開始し、新規ログだけを追跡する。
  停止中コンテナの場合は既存ログのみ表示し、ライブ追跡は開始しない。
- ライブ追跡から到着した行は`_pendingLines`へ追加し、未処理のディスパッチがない場合だけ
  `IUiDispatcher`へ1回の反映処理を予約する。反映時にキューをまとめて`LogLines`へ追加するため、
  行ごとにUIスレッドへディスパッチしない。実アプリでは`DispatcherQueueUiDispatcher`、単体テストでは
  即時実行または記録用ディスパッチャを使う。
- `PauseAsync`中に到着した行は`_pausedBuffer`に保持し、`ResumeAsync`で受信順にディスパッチ待ちキューへ
  戻す。`ClearAsync`は表示中の行、一時停止バッファ、ディスパッチ待ちキューをクリアするが、追跡自体は
  継続する。Open/Clear/Closeでディスパッチトークンを無効化し、既に予約済みの古い反映処理が表示を
  復活させないようにする。
- `LogLines`、`_pausedBuffer`、`_pendingLines`は`BoundedCollection`を使って既定5000要素を上限とし、
  上限超過時は最古の要素から取り除く。
- `CloseAsync`は追跡用`CancellationTokenSource`をキャンセルし、追跡タスクの終了を待ってから
  ディスパッチ待ち状態を破棄してログパネルを閉じる。

## シェルパネルの状態管理

- シェルの状態・ロジックは`ContainerShellViewModel`（`ContainersViewModel.Shell`）が保持する。
  ファサードの`OpenShellCommand`/`SendShellCommandCommand`/`CloseShellCommand`は
  `Shell.OpenAsync`/`SendAsync`/`CloseAsync`へ委譲する。
- `OpenAsync`はシェルパネルを開き、呼び出し時点の`SelectedContainerDetail`が対象IDと一致して停止中なら、
  Application層へ問い合わせず停止状態を表示する。対象コンテナの既存セッションが接続中であれば
  それを再利用し、接続中セッションがない場合は`IContainerManagementService.OpenExecSessionAsync`で新しい
  `IContainerExecSession`を開始する。
- シェルセッションはコンテナID単位で`_execSessions`にキャッシュする。閉じたセッションは再利用せず、
  次回オープン時に削除して作り直す。別コンテナのシェルを開く場合は、現在表示中のセッションを閉じてから
  新しい対象へ切り替える。
- 停止中コンテナなどApplication層がexec開始を拒否した場合は、シェルパネルを開いたまま
  `ShellStatusMessage`へ接続不可状態を表示し、`IsShellError`でエラー状態にする。
- `SendAsync`は現在のセッションへ入力文字列を送信し、送信後に`ShellCommandText`を空にする。
  送信前に末尾のCR/LFだけを取り除くため、Windows側の入力で`ls\r`のような文字列になっても
  シェルへは`ls`として渡る。空白だけの入力、または未接続状態では送信しない。送信に失敗した場合は
  入力欄の内容を保持し、シェルエラーとして表示する。
- `ReadShellOutputAsync`は`IContainerExecSession.ReadOutputAsync`から出力チャンクを受け取り、
  セッション参照付きの`_pendingShellChunks`へ追加する。未処理のディスパッチがない場合だけ
  `IUiDispatcher`へ1回の反映処理を予約し、対象が現在の接続セッションであるチャンクだけをまとめて
  `ShellOutput`へ追加する。行単位ではなくチャンク単位で表示するため、改行で終わらないプロンプトや
  コマンド出力もUIへ反映できる。Open/Close時はディスパッチトークンを無効化し、古いセッションの
  予約済みチャンクが新しい表示へ混入しないようにする。
- `ShellOutput`と`_pendingShellChunks`は`BoundedCollection`を使って既定5000要素を上限とし、
  上限超過時は最古の要素から取り除く。
- 出力ストリームが終了した場合は通常の切断として`IsShellConnected=false`にし、
  `ShellStatusMessage`へ切断状態を表示する。読み取り中の例外はシェルエラーとして表示する。
- `CloseAsync`はシェルパネルを閉じる。接続中セッションがある場合はセッションを閉じ、読み取りタスクの
  終了を待ってから接続状態を解除する。停止中コンテナのようにセッションが作成されていないエラー表示状態でも
  パネルを閉じられる。閉じる操作はコマンド入力行の`Close shell`ボタンから実行できる。

## ログ一覧の自動スクロール

- WinUI公式の「[Inverted lists](https://learn.microsoft.com/en-us/windows/apps/design/controls/inverted-lists)」
  パターンに従い、`LstLogs`（ログ表示用`ListView`）の`ItemsPanel`を`ItemsStackPanel`に差し替え、
  `ItemsUpdatingScrollMode="KeepLastItemInView"`を設定する。これにより「末尾を表示している間は
  新しい行が追加されると自動的に末尾までスクロールする」「途中までスクロールして読んでいる間は
  自動スクロールしない」という受け入れ基準をWinUI自身が保証する。
- `ContainersPage`のコードビハインドはログ表示に関する`ScrollViewer`探索・`ViewChanged`購読・
  `ChangeView`呼び出しやスクロール状態を持たない。末尾追従の判定とスクロール位置の維持は
  `ItemsUpdatingScrollMode`に委ねる。

## ログ/シェルパネルの個別ウィンドウ表示（ポップアウト）

- 右側の補助ペインは小さいため、ログ・シェルパネルのヘッダーに「Open in window」ボタン
  （`BtnOpenLogsWindow`/`BtnOpenShellWindow`）を追加し、同じ内容を大きな個別ウィンドウ
  （`Windows/LogsWindow.xaml`・`Windows/ShellWindow.xaml`）としても表示できる。個別ウィンドウを表示または
  Activateした直後に、対応する小さい補助ペインを非表示にする。これは表示だけを切り替える処理であり、
  `ContainersViewModel.HideLogPanel`/`HideShellPanel`から対応する`Logs`/`Shell`へ委譲する。`LogLines`/
  `ShellOutput`等の状態、ログ追跡、シェルセッションは維持され、個別ウィンドウから引き続き操作できる。
  詳細パネルはこの導線の対象外。
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
  （コンストラクタで受け取り、`ViewModel`プロパティとして公開）。`Logs`と`Shell`はそれぞれ
  1つの選択中コンテナ状態を保持するため、次の制約がある:
  - ポップアウトを開いたまま別コンテナのログ/シェルを開くと、ポップアウト側の表示内容も暗黙に切り替わる。
  - `ShellCommandText`は小さいシェルパネルとポップアウト`ShellWindow`の間でTwoWay共有されるため、
    両方を同時に表示して入力すると干渉し得る。
  - ウィンドウサイズは`RootGrid.Loaded`時に取得した`XamlRoot.RasterizationScale`を用いた近似的な
    DPI考慮のみ（1000x700 DIPs相当）で、モニター間移動時のDPI変化への追従までは行わない。
- ポップアウト側にもログ用のPause/Resume/Clear、シェル用のコマンド入力・Sendを再配置しており、
  ユーザーが`ContainersPage`から離れてポップアウトだけが残っている状態でもセッションを操作できる。
  各ウィンドウ内の`Close`ボタン（`BtnCloseLogs`/`BtnCloseShell`）およびタイトルバーの閉じるボタン
  （×）は、どちらも**このウィンドウを閉じるだけ**で、ログ追跡やシェル接続そのものは停止しない。
  ポップアウトを閉じても非表示にした小さい補助ペインは自動では再表示しない。ログ追跡・シェル接続を
  終了するには、ログ/シェルを再度開いてインラインパネルを表示し、小さい補助ペイン側の
  `CloseLogsCommand`/`CloseShellCommand`に配線されたCloseボタンを使う。ウィンドウを閉じる操作と
  セッションを終了する操作は意図的に分離している。
- `LogsWindow`/`ShellWindow`のタイトルバーは`MainWindow`と同じルック＆フィールにしている。
  `ExtendsContentIntoTitleBar = true`とアプリアイコン付きの`TitleBar`コントロール
  （`IsPaneToggleButtonVisible="False"`、`TitleBarHeightOption.Tall`）を各ウィンドウに配置し、
  `MainWindow`のカスタムタイトルバーと視覚的に統一している（ナビゲーションペインを持たないため
  ペイントグルボタンのみ非表示にしている）。
