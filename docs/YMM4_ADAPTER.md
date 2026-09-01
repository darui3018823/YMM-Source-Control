# YMM4 アダプター接続メモ

このリポジトリの `Core` と WPF プレビューは YMM4 DLL に依存しない。YMM4 へ組み込む段階で、次のような薄いアダプターを別プロジェクトへ追加する。

```csharp
using System;
using YukkuriMovieMaker.Plugin;

namespace YmmSourceControl.Plugin;

public sealed class SourceControlToolPlugin : IToolPlugin
{
    public string Name => "Source Control";

    public bool AllowMultipleInstances => false;

    public Type ViewModelType => typeof(SourceControlToolViewModel);

    public Type ViewType => typeof(SourceControlToolView);
}
```

## 接続時に実装すること

1. YMM4 のプロジェクトディレクトリを取得するサービスを `SourceControlToolViewModel` に注入する。
2. 現在の `MainWindow` の UI を `UserControl` である `SourceControlToolView` に切り出す。
3. `IGitClient` の working directory を、現在の YMM プロジェクトのディレクトリへ向ける。
4. YMM4 のドッキング可能なツールペインとして表示し、初期幅を 300px 前後に設定する。

YMM4 API の型名やプロジェクト情報取得 API は本体バージョンに依存するため、API DLL を実際に参照できる環境で確定させる。Core 側から YMM4 の型を参照しないのは、この変動を閉じ込めるため。

## 配布形態

初期開発では通常の `.dll` として動作確認し、安定後に YMM4 のプラグイン配布形式へまとめる。Git が見つからない場合は、ペイン内にインストール案内を出して YMM4 自体の編集を妨げない。
