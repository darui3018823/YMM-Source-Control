using System.IO;
using System.Windows;

namespace YmmSourceControl.Plugin;

public partial class ConflictResolutionWindow : Window
{
    public ConflictResolutionWindow(string path, string content)
    {
        YmmThemeResources.Initialize();
        InitializeComponent();
        Title = $"競合を手動で解決 - {Path.GetFileName(path)}";
        PathText.Text = path;
        ContentEditor.Text = content;
        ContentEditor.CaretIndex = 0;
    }

    public string ResolvedContent => ContentEditor.Text;

    private void ResolveClick(object sender, RoutedEventArgs e)
    {
        if (ContainsConflictMarker(ResolvedContent))
        {
            MessageBox.Show(
                this,
                "競合マーカーが残っています。<<<<<<<、=======、>>>>>>> の行を確認して削除してください。",
                "競合マーカーを確認",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            this,
            "編集した内容を保存し、このファイルを解決済みとして登録しますか？\n\n選ばなかった内容はこのファイルから削除されます。",
            "競合を解決",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (result == MessageBoxResult.OK)
        {
            DialogResult = true;
        }
    }

    private static bool ContainsConflictMarker(string content)
    {
        return content.Split('\n').Any(line =>
            line.TrimStart().StartsWith("<<<<<<<", StringComparison.Ordinal)
            || line.TrimStart().StartsWith("=======", StringComparison.Ordinal)
            || line.TrimStart().StartsWith(">>>>>>>", StringComparison.Ordinal));
    }
}
