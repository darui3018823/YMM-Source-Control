# コントリビューションガイド

YMM Source Controlに関心を持っていただきありがとうございます。バグ報告、機能提案、ドキュメントの改善、テスト、コードの変更を歓迎します。

すべての参加者は、[行動規範](CODE_OF_CONDUCT.md)を守ってください。

## 作業を始める前に

小さなバグ修正やドキュメントの改善は、そのままPull Requestを作成して構いません。次のような変更は、実装前にIssueで目的と方針を相談してください。

- Git操作の動作や競合解決の仕様を変更するもの
- `.ymmp`や保存状態などのデータ形式・互換性に影響するもの
- UIの大幅な変更やアーキテクチャの変更
- YMM4 API、.NET、主要な依存関係を変更するもの
- Assetsの取り込み、パス変換、Git LFSなどの新しい管理方式

Issueで相談した場合でも、採用やマージを保証するものではありません。プロジェクトの範囲、互換性、保守性を考慮して判断します。

## 開発環境

- Windows
- .NET SDK 10
- Git
- YMM4（プラグインのビルド・動作確認を行う場合）
- GitHub CLI `gh`（Remote発行を確認する場合）

リポジトリを取得した後、プラグインのビルドには`.env.example`を`.env`へコピーし、YMM4本体の場所を`YMM4_ROOT`へ設定してください。`.env`、YMM4本体、個人の素材や認証情報はCommitに含めないでください。

```powershell
Copy-Item .env.example .env
# .env の YMM4_ROOT を設定
./ps1/build-plugin.ps1
```

## ブランチと変更

変更内容が分かる名前のブランチを作成してください。

```text
feature/変更内容
fix/不具合の内容
docs/ドキュメントの内容
refactor/変更内容
```

既存のコードスタイルと設計方針に従い、変更範囲は目的に必要な範囲へ限定してください。UIを変更した場合は、可能であれば変更前後のスクリーンショットを添えてください。

## テストと確認

変更内容に応じて、次のコマンドを実行してください。

```powershell
dotnet test test/YmmSourceControl.Core.Tests/YmmSourceControl.Core.Tests.csproj --configuration Release
dotnet build src/YmmSourceControl.Core/YmmSourceControl.Core.csproj --configuration Release
dotnet build src/YmmSourceControl.App/YmmSourceControl.App.csproj --configuration Release
./ps1/build-plugin.ps1
```

プラグインのビルドは`.env`とYMM4のDLLが必要です。実行できない確認がある場合は、Pull Requestに理由を記載してください。

## Pull Request

Pull Requestには、次の内容を記載してください。

- 変更の目的と内容
- 関連するIssue（あれば）
- 実行したテストと確認方法
- 互換性への影響や既知の制限
- UI変更の場合はスクリーンショット

既存のPull Requestテンプレートも利用してください。レビューコメントには、可能な範囲で対応または理由を説明してください。

## Commit

Commit messageは、特別な理由がなければ次のような形式を推奨します。

```text
feat: 新機能を追加
fix: 不具合を修正
docs: ドキュメントを更新
refactor: 動作を変えずに構成を整理
test: テストを追加または更新
chore: ビルドや開発環境を更新
```

## AIを利用した変更

AIを利用して作成した変更も提出できます。ただし、提出者が変更内容を理解し、正確性、安全性、ライセンス、必要なテストを確認してください。生成された内容を未確認のまま大量に投稿することは避けてください。

## ライセンス

第三者のコード、画像、文章、その他の素材を追加する場合は、利用条件とライセンスを確認し、必要な帰属表示を追加してください。変更を提出する前に、リポジトリの[BSD 3-Clause License](LICENSE)と互換性があることを確認してください。
