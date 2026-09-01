# 開発ガイド

## 必要な環境

- Windows
- .NET SDK 10
- Git
- YMM4 本体（プラグインをビルドまたは実機で確認する場合）

Core とプレビューアプリの開発・テストには YMM4 本体は必要ありません。

## テストとプレビューアプリ

Core のテストは一時 Git リポジトリを使って実行します。現在の作業ツリーや Git の設定を変更しません。

```powershell
dotnet test test/YmmSourceControl.Core.Tests/YmmSourceControl.Core.Tests.csproj --configuration Release
dotnet run --project src/YmmSourceControl.App
```

UI を変更した場合は、プレビューアプリで画面と操作も確認してください。

## YMM4 プラグインのビルド

`.env.example` を `.env` にコピーし、`YMM4_ROOT` に `YukkuriMovieMaker.Plugin.dll` がある YMM4 本体のフォルダーを指定します。

```powershell
Copy-Item .env.example .env
# .env の YMM4_ROOT を実際のパスへ変更
./ps1/build-plugin.ps1
```

既定の出力先は `artifacts/plugin` です。YMM4 SDK は配布物に含めず、GitHub Actions では Core のテストとプレビューアプリのビルドのみを実行します。

## プロジェクト構成

```text
src/YmmSourceControl.Core          Git 操作とモデル
src/YmmSourceControl.Plugin        YMM4 用 WPF プラグイン
src/YmmSourceControl.App           単体プレビュー用 WPF アプリ
test/YmmSourceControl.Core.Tests   Core のテスト
ps1                                ビルド用 PowerShell スクリプト
```
