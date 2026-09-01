using System.Windows;

namespace YmmSourceControl.Plugin;

public partial class BranchOperationDialog : Window
{
    public BranchOperationDialog(
        string title,
        string heading,
        string description,
        string acceptLabel,
        IReadOnlyList<string> branches)
    {
        YmmThemeResources.Initialize();
        InitializeComponent();
        Title = title;
        HeadingText.Text = heading;
        DescriptionText.Text = description;
        AcceptButton.Content = acceptLabel;
        BranchSelector.ItemsSource = branches;
        BranchSelector.SelectedIndex = branches.Count > 0 ? 0 : -1;
    }

    public string TargetBranch => BranchSelector.Text.Trim();

    private void AcceptClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TargetBranch))
        {
            MessageBox.Show(this, "対象ブランチを選択してください。", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }
}
