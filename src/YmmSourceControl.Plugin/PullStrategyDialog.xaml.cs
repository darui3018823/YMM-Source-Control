using System.Windows;
using YmmSourceControl.Core.Models;

namespace YmmSourceControl.Plugin;

public partial class PullStrategyDialog : Window
{
    public PullStrategyDialog(string reason)
    {
        YmmThemeResources.Initialize();
        InitializeComponent();
        ReasonText.Text = reason;
    }

    public PullStrategy Strategy => RebaseOption.IsChecked == true
        ? PullStrategy.Rebase
        : PullStrategy.Merge;

    public bool ShouldRemember => RememberChoice.IsChecked == true;

    private void AcceptClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
