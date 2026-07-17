---
name: store-release-workflow
description: "本リポジトリの新バージョンをMicrosoft Store向けに準備し、最新origin/mainの確認、Semantic Versioning、テスト、manifest更新、x64/ARM64 msixupload生成、バージョン専用PR、GitHub Release、英語のStore新機能文まで安全にオーケストレーションする。Use this skill whenever the user asks to prepare or publish a new version, create a Microsoft Store release, create an msixupload and GitHub Release, bump the Store package version for release, or says 新バージョンをリリース, MS Storeリリース, Storeパッケージを作ってRelease, リリース作業. Do not use for local-only MSIX packaging without a release."
---

# Store Release Workflow

このskillは、本リポジトリ固有のMicrosoft Storeリリースを端から端まで進める。
MSIX生成方法そのものは既存の`winui-packaging` skillと
[`scripts/Build-StoreMsixUpload.ps1`](../../../scripts/Build-StoreMsixUpload.ps1)を再利用し、
ここではバージョン、テスト、PR、公開確認、Release、成果物の公開範囲を統括する。

## スコープ

含むもの:

- 最新`origin/main`を基準にしたリリース準備
- Store package versionの更新
- x64 + ARM64 `.msixupload`の生成と検証
- バージョン更新だけを含むPRの作成とマージ
- 前回Releaseからの差分を記載したGitHub Release
- Microsoft Store「What's new」欄向けの簡潔な英語箇条書き

含まないもの:

- Partner Centerへのブラウザーアップロードや申請送信
- ローカル確認だけを目的とするMSIX生成（`winui-packaging` skillを直接使う）
- 公開配布用のsideload package作成（必要なら別タスクとして明示的に扱う）
- リリースと無関係な不具合修正や機能変更

## 前提条件

- `gh auth status`が成功し、対象リポジトリでPRのマージとRelease作成ができること。
- `dotnet`と`winapp`がPATHに存在すること。
- `winui-packaging` skillと`dotnet-test`系のテストskill/agentが利用できること。
- Store提出済みの最大package versionが不明な場合は、Partner Centerで確認するかユーザーに確認すること。
  Store package versionは過去に提出した値より大きくなければならない。

## 絶対ルール

1. **`.msixupload`を公開GitHub Releaseへ添付しない。**
   これはPartner Center提出用の未署名成果物であり、一般ユーザー向け配布物ではない。
   ローカルまたは非公開artifact storageに保持し、パスとSHA-256だけをユーザーへ返す。
2. **`.pfx`、証明書パスワード、秘密鍵をcommit・push・Release添付しない。**
3. **テスト失敗時は公開を停止する。**
   `origin/main`にも存在する既知の失敗でも自動続行せず、失敗内容を示してユーザーの明示承認を得る。
4. **バージョンを無断で決めない。**
   未指定時はSemantic Versioning候補を提示し、ユーザー確認後に更新する。
5. **PRのマージ直前とReleaseの公開直前に、それぞれ`ask_user`で確認する。**
   `ask_user`を利用できない実行環境ではそこでハードストップし、明示承認を推測して続行しない。
6. **GitHub Releaseはマージ済み`origin/main`のコミットをtag付けする。**
   未マージbranchやpackage生成時の一時commitを直接tag付けしない。
7. **リリースPRのリポジトリ差分はmanifestのバージョン1行だけにする。**
   生成物、Release notes、作業メモをcommitしない。

## ワークフロー

### 1. Preflight

1. `git fetch origin --prune --tags`を実行する。
2. `git status --short --branch`で作業ツリーがcleanであることを確認する。
3. 現在のbranchのtreeが`origin/main`と一致することを確認する。
   一致しない場合は、ユーザー変更を破棄せず、最新`origin/main`を基点にできる状態へ整える。
4. Copilot workspaceでは現在のsession branchをリリース専用branchとして使う。
   リリースと無関係なcommitがある場合はbranchを書き換えず、`origin/main`から新しいsessionを作る。
5. `gh release list`、tag一覧、前回Releaseの本文と対象commitを取得する。
6. 前回tagから`origin/main`までのcommit、PR、changed filesを調べる。

次のいずれかなら停止する:

- 未commitの変更がある
- `origin/main`をfetchできない
- 同名のtarget tagまたはReleaseが既にある
- 現在のbranchに今回のリリース以外の差分がある

### 2. バージョン決定

ユーザーがバージョンを指定していない場合は、前回Releaseとの差分から候補を決める。

| 差分 | 候補 |
|---|---|
| 互換性を壊す変更 | Major |
| ユーザー向け新機能 | Minor |
| 不具合修正、文書、ビルド/配布改善のみ | Patch |

候補と根拠を示し、`ask_user`で確認する。

- Git tag / GitHub Release: `vX.Y.Z`
- [`Package.appxmanifest`](../../../src/WslContainersDesktop.App/Package.appxmanifest): `X.Y.Z.0`

4要素目はStore向けrevisionとして`0`を使う。ユーザーが4要素版だけを指定した場合は、その値を尊重しつつ、
対応する3要素のGit tag / GitHub Release versionも`ask_user`で確認する。

### 3. 変更前テスト

1. `dotnet-test`の既存skill/agentを使い、現在の`origin/main`相当treeで全テストを実行する。
2. 実行command、成功数、失敗数、skip数を記録する。
3. Release configurationがリポジトリ既存設定により実行不能な場合も、成功扱いにせず失敗として提示する。
4. 1件でも失敗した場合は公開フローを停止し、`ask_user`で続行可否を確認する。

承認なしに、テストをskipするflag、対象除外filter、成功に見せるfallbackを追加してはならない。
リリースと無関係な失敗をこの作業へ混ぜて修正しない。

### 4. manifestのバージョン更新

1. `quick-fix` agentへ、`Package.appxmanifest`の`Identity.Version`だけを
   `Version="<old>"`から`Version="<new>"`へ厳密に置換し、BOMと他の文字列を保持するよう依頼する。
2. `git diff --check`を実行する。
3. `git diff --name-only`と完全なdiffを確認する。

この時点の許容差分:

```text
src/WslContainersDesktop.App/Package.appxmanifest
```

バージョン以外の差分があればcommitせずに停止する。

### 5. Store package生成

1. `winui-packaging` skillを読み込む。
2. リポジトリ既定scriptを実行する。

```powershell
./scripts/Build-StoreMsixUpload.ps1
```

既定のx64 + ARM64を使う。Store提出物には`-Sign`を付けない。
scriptが次を完了したことを確認する:

- Release / self-contained publishがx64とARM64の両方で成功
- `.msixbundle`に両architectureが存在
- 各packageのmanifest image検証が成功
- `.msixupload`直下に期待する`.msixbundle`が1つ存在
- ファイル名と内部manifestが確定済みversionを示す

追加で次を記録する:

- `.msixupload`の絶対パス
- byte size
- SHA-256
- `git check-ignore`により`AppPackages/`がgitignore対象であること

package生成後、リポジトリ差分がmanifest 1行のままであることを再確認する。

### 6. 変更後テスト

変更前と同じテストsuiteを同じ条件で再実行する。package build成功だけで単体テストを代替しない。

- 新しい失敗があれば停止する。
- 変更前からの失敗を承認済みでも、件数や内容が変わった場合は再確認する。
- package検証失敗時はPRやReleaseを作らない。

### 7. バージョン専用PR

1. manifestだけをstageする。
2. commit messageにversionを含め、実行環境が指定するcommit trailerを付ける。
3. branchをpushし、`gh pr create`で`main`向けPRを作成する。
4. PR本文には変更したversionとpackage検証結果を書く。`.msixupload`のローカル絶対パスは書かない。
5. `gh pr view`でchanged filesがmanifest 1ファイル、1行置換だけであることを確認する。
6. PR checksとmergeabilityを確認する。
7. PR URLとmerge予定commitを示し、`ask_user`でマージ確認を取る。
8. 承認後、リポジトリの既存履歴に合わせてsquash mergeする。

checksが構成されていない場合は待機対象なしとして記録する。checks失敗、review待ち、merge conflict、
branch protectionがある場合は回避せず停止する。

### 8. Release notesとStore文面

前回tagからマージcommitまでのPR本文とdiffを根拠に、Release notesをsession artifactとして作る。
commit titleだけで内容を推測しない。

Release notesには次を含める:

1. リリース概要
2. ユーザー向け新機能
3. 修正とUI改善
4. Store/配布関連の変更
5. `v<previous>...v<current>`のFull changelog link
6. Microsoft Store「What's new」向け英語箇条書き

Store箇条書きは簡潔なユーザー価値だけを書く。内部class名、PR番号、TDD、agent、package script等は書かない。

Release notesに「attached `.msixupload`」や公開download link、ローカルpath、秘密情報を書かない。

### 9. 公開前整合性確認

PRマージ後に再度fetchし、次を確認する:

- PRが`MERGED`
- `origin/main`のmanifest versionが確定値
- packageを作ったtreeとマージ後`origin/main`のtreeが同一
- target tagが未作成
- Release notesの比較範囲が正しい

treeが異なる場合は、マージ後`origin/main`と同じtreeからpackageを再生成してSHA-256を更新する。

公開予定のversion、target commit、Release notes概要、GitHub Releaseへassetを添付しないことを示し、
`ask_user`で公開確認を取る。

### 10. GitHub Release公開

承認後、`gh release create`を使い、マージ済み`origin/main` commitを明示してReleaseを作る。

```powershell
gh release create "vX.Y.Z" `
  --target "<origin-main-commit>" `
  --title "Version X.Y.Z" `
  --notes-file "<session-artifact-release-notes>"
```

**asset引数を渡さない。** `.msixupload`はReleaseへuploadしない。

公開後に次を検証する:

- draft/prereleaseではない
- tag targetが期待する`origin/main` commit
- Latest Releaseとして表示される
- Release bodyに前回tagからの差分とFull changelogがある
- `assets`が空

### 11. 最終出力

次の形式で簡潔に返す:

```markdown
**vX.Y.Z をリリースしました。**

- Store package: `<private-or-local-path>` (x64 / ARM64)
- SHA-256: `<hash>`
- Pull request: `<url>` (merged)
- GitHub Release: `<url>` (public assets: none)

**Microsoft Store - What's new**

- <English user-facing change>
- <English user-facing change>
```

ローカルpathがsession終了時に失われる可能性がある場合は、それを明示し、ユーザー指定の非公開保存先へ
移す必要があることを伝える。Partner Centerへの提出はユーザーが行う。

## エラー処理

| 状況 | 対応 |
|---|---|
| `gh`未認証/権限不足 | `gh auth status`の結果を示し、認証または権限付与後に再開 |
| `dotnet`または`winapp`がない | `winui-packaging`の前提手順を案内し、tool導入前に続行しない |
| test失敗 | 公開を停止し、失敗内容とbaseline比較を示して明示承認を求める |
| package/image検証失敗 | PR/Releaseを作らず、scriptの具体的な失敗箇所を直す別タスクに分離 |
| version/tagが既存 | 上書きやtag移動をせず、新versionをユーザーと決め直す |
| PRにmanifest以外の差分 | 余分な変更を勝手に破棄せず、分離方法をユーザーへ確認 |
| PR checks失敗/merge不可 | bypassせず停止し、GitHubの状態を報告 |
| merge後treeがpackage生成時と違う | 最新`origin/main`からpackageを再生成 |
| `.msixupload`を誤って公開 | `gh release delete-asset`で直ちに削除し、Release本文の添付記述とchecksumも除去して再確認 |
| Release作成途中でasset upload失敗 | 公開Releaseに不完全なasset説明を残さず、`.msixupload`なしのReleaseへ修正 |

## 振り返り

完了後、実際の手順とこのskillの差分、繰り返し発生した失敗、不要だったtool callを確認する。
リポジトリ固有の再現可能な改善があれば、このskillを更新する別PRとして提案する。
個別リリース固有の値や一時的な障害はskillへ固定しない。

## 参照

| 対象 | 用途 |
|---|---|
| [`scripts/Build-StoreMsixUpload.ps1`](../../../scripts/Build-StoreMsixUpload.ps1) | Store package生成とimage検証の正本 |
| [`Package.appxmanifest`](../../../src/WslContainersDesktop.App/Package.appxmanifest) | Store identity/versionの正本 |
| [`AGENTS.md`](../../../AGENTS.md) | モデルルーティング、既存skill再利用ルール |
| `winui-packaging` skill | WinUI packagingの一般手順とtool前提 |
