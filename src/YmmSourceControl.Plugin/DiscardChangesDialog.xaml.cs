using System.Windows;

namespace YmmSourceControl.Plugin;

public partial class DiscardChangesDialog : Window
{
    public DiscardChangesDialog(string filePath)
    {
        InitializeComponent();
        FilePath = filePath;
        MessageText.Text = $"「{filePath}」の変更を破棄します。この操作は元に戻せません。";
    }

    public string FilePath { get; }

    private void DiscardClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
