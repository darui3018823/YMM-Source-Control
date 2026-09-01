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
- commit
- ファイル変更を検知した自動更新（短い debounce 付き）

## 起動

```powershell
dotnet run --project src/YmmSourceControl.App
```

Git リポジトリの中で起動すると、そのリポジトリを対象にします。リポジトリ外では、ペインに案内が表示されます。

## YMM4 への接続

YMM4 の API DLL を参照できる環境で、`IToolPlugin` のアダプターを追加します。現行の API では `ViewType` と `ViewModelType` を返すツールプラグインとして登録する方針です。

詳細な設計と v1/v2 の境界は [docs/PROPOSAL.md](docs/PROPOSAL.md) を参照してください。

## YMM4 プラグインとしてビルド

YMM4 本体のディレクトリを指定して、プラグインを Release ビルドします。

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
