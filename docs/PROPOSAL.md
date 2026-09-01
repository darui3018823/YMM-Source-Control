# YMM Source Control 草案

## 目的

YMM4 のプロジェクトを Git で管理する際に、ターミナルへ移動せず、編集の流れの中で変更確認からコミットまで行えるようにする。

狙いは「Git GUI を作ること」ではなく、動画編集者が現在の作業状態を理解しやすい Source Control ペインを提供すること。

## v1 の体験

```text
SOURCE CONTROL                         280〜340px を基準
────────────────────────────────────
main                              ⟳  ⋯

Message
┌──────────────────────────────────┐
│ 字幕を修正                        │
└──────────────────────────────────┘
[ Commit ]

CHANGES (3)
  M  project.ymmp
  M  assets/reimu.png
  ?  notes.txt

STAGED CHANGES (1)
  M  README.md
```

ペインは `IToolPlugin` で登録するドッキング可能なツールペインとする。Activity Bar のような YMM 本体への領域注入は v1 の対象外にする。

## UI の判断

- 常用幅は 280〜340px、最低幅は約 220px
- 狭い場合はディレクトリを省略し、ファイル名と状態を優先
- 変更件数、ブランチ、同期操作を上部に集約
- `.ymmp` は通常のファイル行として扱い、semantic diff は行選択後の詳細ビューで段階的に導入
- discard は誤操作防止の確認を必須にする

## 技術構成

```text
YMM4 IToolPlugin adapter
          ↓
WPF View / ViewModel
          ↓
Core: SourceControlService
          ↓
GitClient ── Process ── git CLI
          ↑
FileSystemWatcher + debounce
```

Git ライブラリではなく CLI を使う。Credential Manager、`.gitconfig`、署名、hooks、attributes など、ユーザーが既に使っている Git の挙動をそのまま利用するため。

## v1 スコープ

- リポジトリ自動検出（YMM プロジェクトのディレクトリから親方向へ探索）
- 現在ブランチ表示
- Changes / Staged の取得
- stage / unstage / discard
- commit message と commit
- diff の表示
- pull / push / sync
- ファイル変更時の自動 refresh

## v2 スコープ

`.ymmp` の JSON 差分を YMM の概念に変換する。

```text
Scene 3
  字幕「今日はカレーです」
    Text: 「今日はカレーです」 → 「今日はラーメンです」
  BGM bgm.wav
    Volume: 80 → 65
  + SE explosion.wav
```

この段階で、単なる VS Code 風 Git UI から「動画編集向け Git クライアント」へ差別化できる。ID の追跡、正規化、3-way merge は semantic diff の設計が固まってから追加する。

## 未決事項

1. YMM4 のプロジェクトディレクトリを API から取得する方法
2. ツールペインの初期幅・タイトル・アイコンの登録方法
3. `.ymmp` の schema とアイテム ID の安定性
4. discard で対象にするファイルの扱い（未追跡ファイルを含めない）
5. Git 未インストール時の導線

## 最初の実装単位

このリポジトリでは、まず `Core` と単体 WPF アプリを完成させる。その後、YMM4 API DLL の参照を持つ薄い `IToolPlugin` アダプターを追加する。これにより、YMM4 がなくても Git parser と UI の検証を進められる。

