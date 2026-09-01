using System.Windows;

namespace YmmSourceControl.Plugin;

public partial class SmartCommitDialog : Window
{
    public SmartCommitDialog()
    {
        YmmThemeResources.Initialize();
        InitializeComponent();
    }

    public SmartCommitPreference Choice { get; private set; } = SmartCommitPreference.Ask;

    private void StageAndCommitClick(object sender, RoutedEventArgs e)
    {
        Choice = SmartCommitPreference.Ask;
        DialogResult = true;
    }

    private void AlwaysClick(object sender, RoutedEventArgs e)
    {
        Choice = SmartCommitPreference.Always;
        DialogResult = true;
    }

    private void NeverClick(object sender, RoutedEventArgs e)
    {
        Choice = SmartCommitPreference.Never;
        DialogResult = false;
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        Choice = SmartCommitPreference.Ask;
        DialogResult = false;
    }
}
