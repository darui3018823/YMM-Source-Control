# YMM Source Control

YukkuriMovieMaker 4 に、Git の変更確認・ステージング・コミットをまとめた Source Control ペインを追加する草案です。

## この草案の方針

- UI は VS Code の Source Control に寄せる
- Git の実体は `git` CLI に任せる
- `.ymmp` は通常の Git 管理を維持しつつ、将来は YMM 向け semantic diff を追加する
- Git 操作と YMM4 の `IToolPlugin` 接続を分離する
- まず単体の WPF プレビューで UI と Git 操作を検証する

## 現在できること

- リポジトリの自動検出
- 現在ブランチの表示
- Changes / Staged の表示
- stage / unstage / discard
- commit / amend / commit and push
- fetch / pull / push
- Merge または Rebase を選択する Pull（選択の保存に対応）
- ブランチを指定した Merge / Rebase
- Merge / Rebase 競合の片側採用・手動編集、続行、中止
- upstream と ahead / behind の表示
- `gh` CLI による GitHub Remote の発行
- ファイル変更を検知した自動更新（短い debounce 付き）

## Remote操作

Remoteがあるリポジトリでは、Source ControlペインからFetch、Pull、Pushを実行できます。PullではMergeまたはRebaseを選択でき、「この選択を保存し、次回から確認しない」を有効にすると、そのツール状態へ方式を保存します。

Pushがnon-fast-forwardで拒否された場合は、MergeまたはRebaseでRemoteの変更を取り込んでからPushを再試行します。強制Pushは実行しません。

MergeまたはRebaseで競合したファイルはペイン内の競合セクションへ表示されます。ファイルごとにローカルまたはRemoteの内容を採用するほか、「手動で編集」から競合マーカーを含むファイルを編集できます。すべて解決した後に操作を続行でき、操作全体を中止して開始前へ戻すこともできます。

## GitHubへRemoteを発行

Remoteがないリポジトリでは「GitHubへRemoteを発行」からGitHubリポジトリを作成できます。認証処理はプラグイン内に持たず、PATH上のGitHub CLI (`gh`) とその既存ログイン状態を使用します。

```powershell
gh auth login
```

発行ダイアログではリポジトリ名、Remote名、説明、公開範囲、作成後にPushするかを指定できます。

## 起動

```powershell
dotnet run --project src/YmmSourceControl.App
```

Git リポジトリの中で起動すると、そのリポジトリを対象にします。リポジトリ外では、ペインに案内が表示されます。

## YMM4 への接続

YMM4 の API DLL を参照できる環境で、`IToolPlugin` のアダプターを追加します。現行の API では `ViewType` と `ViewModelType` を返すツールプラグインとして登録する方針です。

詳細な設計と v1/v2 の境界は [docs/PROPOSAL.md](docs/PROPOSAL.md) を参照してください。

## YMM4 プラグインとしてビルド

`.env.example` を `.env` にコピーし、YMM4 本体のディレクトリを `YMM4_ROOT` に設定します。

```powershell
Copy-Item .env.example .env
# .env の YMM4_ROOT を実際の YMM4 本体ディレクトリへ変更
```

設定後はスクリプトから簡単に Release ビルドできます。

```powershell
./ps1/build-plugin.ps1
```

Debug ビルドや出力先の変更も指定できます。

```powershell
./ps1/build-plugin.ps1 -Configuration Debug -OutputPath artifacts\plugin-debug
```

スクリプトを使わず、YMM4 本体のディレクトリを直接指定してビルドする場合は次のコマンドです。

```powershell
dotnet build src/YmmSourceControl.Plugin/YmmSourceControl.Plugin.csproj `
  -c Release `
  -p:Ymm4Path="C:\path\to\YukkuriMovieMaker_v4_Lite" `
  -o artifacts\plugin
```

`YmmSourceControl.dll` と `YmmSourceControl.Core.dll` を、YMM4 の次のフォルダーへ同じように配置します。

```text
<YMM4>\user\plugin\YmmSourceControl\
```

この環境では既に `C:\Users\daruks\Downloads\YukkuriMovieMaker_v4_Lite\user\plugin\YmmSourceControl\` へ配置済みです。YMM4 の再起動後に読み込まれます。
