# Presentation層: 設定ViewModelとWSL設定の永続化

> このドキュメントは現時点のスナップショットです。経緯・検討過程は書きません。
> 採用理由は [ADR-0011](../adr/0011-do-not-auto-restart-wsl-on-settings-change.md) と
> [ADR-0012](../adr/0012-wsl-environment-detection-and-wslconfig-editing.md) を参照してください。

## 概要

設定画面は初期版として2カテゴリを扱う（[`docs/specs/0007-settings.md`](../specs/0007-settings.md)）。

1. **WSL連携状態の確認**（読み取りのみ）: WSLバージョン検出と、WSL Containersの動作要件充足判定。
2. **リソース制限**（読み取り/変更/保存/リセット）: メモリ量(MB)と論理プロセッサ数を
   `%USERPROFILE%\.wslconfig` の `[wsl2]` セクションに永続化する。

`SettingsViewModel`（`ViewModels/SettingsViewModel.cs`）はApplication層の`ISettingsService`にのみ
依存し、`.wslconfig`や`wsl.exe`の具体的な扱いには依存しない。

## 層をまたぐ責務分担

- **Domain** — `WslEnvironmentInfo`（環境の生の事実: WSLバージョン、wslc有無）、
  `WslIntegrationStatus`（生の事実＋要件充足フラグ）、`WslResourceLimits`（メモリMB/プロセッサ数、
  `null`はWSL既定を表す）。`WslResourceLimits.IsValid`は各値が`null`または正であることを表し、
  `Defaults`は両方`null`。
- **Application** — `ISettingsService`（Inboundポート）、`IWslEnvironmentProbe`/`IWslResourceLimitsStore`
  （Outboundポート）。`SettingsService`が**要件判定ポリシー（WSL 2.9.3以上 かつ wslc利用可能）を所有**する。
  Infrastructureは生の事実のみを返す。
- **Infrastructure** — `WslEnvironmentProbe`（`wsl --version`出力とwslc有無から事実を観測）、
  `WslConfigResourceLimitsStore`（`.wslconfig`の`[wsl2]`を読み書き）。実際のプロセス起動・ファイルI/Oは
  低レベルseam（`IWslCommandRunner`/`IWslcExecutableProbe`/`IWslConfigFileAccessor`）に隔離する。
- **Presentation** — `SettingsViewModel`と`SettingsPage`。

## SettingsService（要件ポリシーとオーケストレーション）

- `GetIntegrationStatusAsync`は`IWslEnvironmentProbe`の生の事実を取得し、
  `wslc利用可能 && Version.TryParse(WSLバージョン) >= 2.9.3` を要件充足として評価する。
  バージョン比較は文字列比較ではなく`Version`型で行う（例: `2.10.0 >= 2.9.3` は真）。
- `GetResourceLimitsAsync`は`IWslResourceLimitsStore.GetAsync`へ委譲する。
- `SaveResourceLimitsAsync`は (1) 要件を再取得し未達なら`WslRequirementsNotMetException`、
  (2) `!limits.IsValid`なら`InvalidResourceLimitsException` を送出する（いずれもstoreを呼ばない）。
  両方を満たした場合のみ`store.SaveAsync`を呼ぶ。
- `ResetResourceLimitsAsync`は要件を再チェックした上で`store.SaveAsync(WslResourceLimits.Defaults)`を呼ぶ。

## WSL環境検出（WslEnvironmentProbe）

- `wsl --version` を引数`["--version"]`で実行し、標準出力から最初の `\d+\.\d+\.\d+(?:\.\d+)?` を
  バージョン文字列として抽出する。ラベル文言（例: 英語 `WSL version:` / 日本語 `WSL バージョン:`）に
  依存せずロケール非依存に解析するため、意図的に「行内の最初のバージョン様トークン」を採用している
  （`wsl --version` はWSLバージョンを先頭行に出力する）。
- 状態確認用途のため回復性を優先する。終了コードが非0、プロセス起動失敗（WSL未インストール等）、
  バージョン非検出のいずれの場合もバージョンを`null`として扱い、例外は送出しない
  （`OperationCanceledException`のみ再送出）。
- wsl.exeの標準出力はUTF-16LEで返るため、低レベルseamの実装で相応にデコードする。

## .wslconfig の読み書き（WslConfigResourceLimitsStore）

読み取り（`GetAsync`）:

- ファイルが無い/空の場合は`WslResourceLimits.Defaults`を返す。
- INI形式を解析し、`[wsl2]` セクション（大文字小文字無視）内の `memory` / `processors` のみを読む。
  セクション/キーの照合は大文字小文字を無視し、`=` 周辺の空白を許容する。
- `#` / `;` で始まる行と空行は無視する。空値のキーは未指定（`null`）として扱う。
- `memory`は単位付きを正規化してMB整数に変換する（`mb`/単位なし=そのまま、`gb`=×1024、`kb`=÷1024、
  `tb`=×1024×1024）。換算は`checked`で行い、オーバーフローや`int`範囲外（≤0 または `int.MaxValue`超過）
  になる値は、対象の生値を含む`WslSettingsAccessException`を送出する（例: `4096TB`は黙って0へ丸めない）。
  解釈できない`memory`/`processors`値も同様に`WslSettingsAccessException`を送出する。
- 同一 `[wsl2]` が複数ある場合は後勝ち。ファイルアクセス失敗は`WslSettingsAccessException`で
  ラップし、内部例外を保持する。

書き込み（`SaveAsync`）:

- 既存内容を読み、他セクション・他キー・コメント・改行を保全したまま、全 `[wsl2]` セクション内の
  既存 `memory`/`processors` 行を除去し、最初の `[wsl2]`（無ければ生成）へ新しい値を挿入する。
- 改行コードは既存内容から推定する（`\n`のみなら`\n`、それ以外は`\r\n`）。
- `memory`は常に `<n>MB` として書く。`null`のキーは書かない（`Defaults`保存で両キーが除去される）。
- 書き込み失敗は`WslSettingsAccessException`で内部例外を保持してラップする。
- 実ファイルは`IWslConfigFileAccessor`が`%USERPROFILE%\.wslconfig`に対して原子的に書く。
  一意な一時ファイル名（GUID付き）へ書き出してから`Move`で置換し、書き込みが衝突しないようにする。

## SettingsViewModel の状態管理

- 連携状態: `WslVersionText`（未検出時は`NotDetectedText`）、`IsWslContainersAvailable`、
  `MeetsRequirements`、`IsWslDetected`。派生:
  `CanEditResourceLimits => MeetsRequirements && HasLoadedResourceLimits && !IsSaving`、
  `IsRequirementsWarningVisible => HasCheckedRequirements && !MeetsRequirements`。
  `HasLoadedResourceLimits`はリソース制限の読み込みに成功したときのみ真になり、読み込み失敗時に
  空欄・古い入力で`.wslconfig`を上書きしてしまうのを防ぐ。
- 入力: `MemoryMegabytesInput` / `ProcessorCountInput`（TwoWayの文字列、空欄はWSL既定=`null`）。
- メッセージ: `ErrorMessage` / `StatusMessage` と対応する可視フラグ。`IsLoading` / `IsSaving`。
- `RefreshAsync`は開始時に`HasLoadedResourceLimits`を偽へ戻し、連携状態を取得して各プロパティへ反映、
  `HasCheckedRequirements`を立ててから現在のリソース制限を読み込み、成功時にのみ`HasLoadedResourceLimits`を
  立てる。失敗時は`ErrorMessage`に例外メッセージを設定する（リソース読込に失敗した状態では編集不可）。
- `SaveAsync`は要件未達なら何もせず案内、非空かつ非整数または0以下なら不正入力エラー（サービス未呼び出し）。
  正常時は保存後にstoreから再読込して入力欄を正規化表示し（例: `08192`→`8192`）、保存済みメッセージを表示する。
- `ResetAsync`は要件未達なら何もせず案内、正常時はリセット後に再読込して入力欄を空にし、リセット済み
  メッセージを表示する。
- 入力の解析・表示は`CultureInfo.CurrentCulture`を用いる。

## 反映と再起動の案内（ADR-0011）

- 保存/リセットは`.wslconfig`を書くだけで、アプリは`wsl --shutdown`を**自動実行しない**（実行中の
  WSL/コンテナを巻き添えにしないため）。
- 保存/リセット成功時のステータスメッセージには、変更反映のためWSLの再起動（`wsl --shutdown`）が
  必要である旨を含める。

## SettingsPage（View）

- ヘッダー（4pxアクセントバー付きカード）、連携状態カード（WSLバージョン表示、wslc利用可否、要件未達時の
  警告`InfoBar`）、リソース制限カード（メモリ/プロセッサ入力、Save/Resetボタン、
  `CanEditResourceLimits`で有効化）、エラー`InfoBar`と成功`InfoBar`で構成する。
- 入力欄は`Mode=TwoWay, UpdateSourceTrigger=PropertyChanged`でバインドし、空欄=WSL既定であることを
  ヒントで示す。
- Saveボタンは`SaveCommand`にバインドし、Resetボタンは`ContentDialog`で確認を取ってから
  `ResetCommand`を実行する。
- ページはDIコンテナから`SettingsViewModel`を解決し、`Loaded`で`RefreshCommand`を実行する。
- 全操作コントロールに`AutomationProperties.AutomationId`を付与し、ユーザー向け文字列は`x:Uid`で
  ローカライズリソースから供給する。
