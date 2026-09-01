# YMM Source Control

YukkuriMovieMaker 4（YMM4）で Git リポジトリを操作するための Source Control プラグインです。変更の確認、ステージング、コミット、Remote 操作、Merge / Rebase の競合解決を YMM4 のツールペインから行えます。

## 概要

- YMM4 の `IToolPlugin` として Source Control ツールを提供
- Git の実行はインストール済みの `git` コマンドへ委譲
- GitHub への Remote 発行はインストール済みの `gh` コマンドを使用
- WPF のプレビューアプリで、YMM4 と切り離して Git 操作と UI を確認可能

## 対応している操作

### リポジトリと変更

- Git リポジトリの自動検出
- 対象フォルダーの選択
- Git リポジトリの初期化
- 現在のブランチとリポジトリ状態の表示
- Changes / ステージ済みの変更の表示
- ファイル単位のステージ、ステージ解除、変更破棄
- ファイルの差分とコミット履歴の確認

### コミット

- コミット
- 直前のコミットの修正（amend）
- コミットして Push
- ステージ済みの変更がない場合のステージ確認

### Remote 操作

- Fetch
- Pull（Merge / Rebase の選択と設定保存）
- Push
- Push が拒否された場合の Pull → Push 再試行
- ブランチを指定した Merge / Rebase
- GitHub への Remote 発行

Fetch、Pull、Push、Commit の実行中は、処理中のボタンにスピナーを表示し、同時に実行できない操作を無効化します。強制 Push（`--force`）は実行しません。

### 競合解決

Merge または Rebase で競合が発生すると、競合ファイルを一覧表示します。

- `PCを残す`：この PC 側の内容を採用
- `共有先を使う`：Remote 側の内容を採用
- `手動で編集`：競合マーカーを含むファイルを編集
- `中止`：Merge / Rebase を開始前の状態へ戻す
- すべての競合を解決した後に `続行`

手動編集では `<<<<<<<`、`=======`、`>>>>>>>` の競合マーカーが残っていると登録できません。編集した内容を保存し、解決済みとしてステージしてから処理を続行します。

## 素材ファイルの扱い

現在のバージョンでは、Git が追跡するのは `.ymmp` などのプロジェクトファイルです。YMM4 が参照する動画・音声・画像などの素材は自動でコピーせず、外部参照のまま扱います。

素材が絶対パスで保存されている場合、同じ PC の同じ場所に素材が存在する環境でのみ、そのプロジェクトをそのまま開けます。素材の取り込み、参照パスの書き換え、Git LFS による素材管理は未対応です。

## 必要な環境

- Windows
- .NET SDK 10
- Git
- YMM4（YMM4 プラグインとして使用する場合）
- GitHub CLI (`gh`)（GitHub へ Remote を発行する場合）

## プレビューアプリの起動

Git リポジトリの中で起動すると、そのリポジトリを対象にします。

```powershell
dotnet run --project src/YmmSourceControl.App
```

## テストの実行

Core の Git 操作とモデルは、次のコマンドでテストできます。テストでは一時フォルダー内に Git リポジトリを作成するため、作業中のリポジトリは変更しません。

```powershell
dotnet test test/YmmSourceControl.Core.Tests/YmmSourceControl.Core.Tests.csproj --configuration Release
```

## YMM4 プラグインのビルド

`.env.example` を `.env` にコピーし、`YMM4_ROOT` に YMM4 本体のディレクトリを設定します。指定先には `YukkuriMovieMaker.Plugin.dll` が必要です。

```powershell
Copy-Item .env.example .env
# .env の YMM4_ROOT を実際の YMM4 本体ディレクトリへ変更
./ps1/build-plugin.ps1
```

既定では Release ビルドを `artifacts\plugin` へ出力します。構成と出力先は変更できます。

```powershell
./ps1/build-plugin.ps1 -Configuration Debug -OutputPath artifacts\plugin-debug
```

ビルド後、次のファイルを YMM4 の Plugin フォルダーへ配置します。

```text
<YMM4>\user\plugin\YmmSourceControl\
├─ YmmSourceControl.dll
├─ YmmSourceControl.Core.dll
├─ YmmSourceControl.deps.json
└─ YmmSourceControl.Core.deps.json
```

配置後に YMM4 を再起動するとプラグインが読み込まれます。

直接 `dotnet build` を実行する場合は、YMM4 本体のディレクトリを MSBuild プロパティで指定します。

```powershell
dotnet build src/YmmSourceControl.Plugin/YmmSourceControl.Plugin.csproj `
  -c Release `
  -p:Ymm4Path="C:\path\to\YukkuriMovieMaker_v4_Lite" `
  -o artifacts\plugin
```

## プロジェクト構成

- `src/YmmSourceControl.Core`：Git 操作とモデル
- `src/YmmSourceControl.Plugin`：YMM4 プラグインと Source Control UI
- `src/YmmSourceControl.App`：単体プレビュー用 WPF アプリ
- `ps1`：ビルド用 PowerShell スクリプト

## 開発・貢献

- [コントリビューションガイド](CONTRIBUTING.md)
- [行動規範](CODE_OF_CONDUCT.md)
- [セキュリティポリシー](SECURITY.md)

## License

BSD 3-Clause License。詳細は [LICENSE](LICENSE) を参照してください。
