using System.Windows;
using YmmSourceControl.Core.Models;

namespace YmmSourceControl.Plugin;

public partial class FileDiffWindow : Window
{
    public FileDiffWindow()
    {
        InitializeComponent();
    }

    public void SetDiff(GitFileStatus file, IReadOnlyList<GitCommitChange> changes, string diff)
    {
        Title = $"変更内容 - {file.FileName}";
        FileNameText.Text = file.FileName;
        PathText.Text = file.Path;
        StatusText.Text = file.IsStaged ? "ステージ済み" : "未ステージ";
        ChangeCountText.Text = $"{changes.Count} 件";
        SummaryList.ItemsSource = changes;
        DiffText.Text = string.IsNullOrWhiteSpace(diff)
            ? "このファイルに表示できる差分はありません。"
            : diff;
    }
}
