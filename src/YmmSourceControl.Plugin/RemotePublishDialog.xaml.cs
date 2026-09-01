using System.Windows;
using YmmSourceControl.Core.Models;

namespace YmmSourceControl.Plugin;

public partial class RemotePublishDialog : Window
{
    public RemotePublishDialog(string repositoryName)
    {
        InitializeComponent();
        RepositoryNameText.Text = repositoryName;
        RepositoryNameText.SelectAll();
        RepositoryNameText.Focus();
    }

    public RemotePublishOptions? Options { get; private set; }

    private void PublishClick(object sender, RoutedEventArgs e)
    {
        var repositoryName = RepositoryNameText.Text.Trim();
        var remoteName = RemoteNameText.Text.Trim();
        if (string.IsNullOrWhiteSpace(repositoryName) || string.IsNullOrWhiteSpace(remoteName))
        {
            ValidationText.Text = "リポジトリ名とRemote名を入力してください。";
            return;
        }

        var visibility = PublicOption.IsChecked == true
            ? RemoteVisibility.Public
            : InternalOption.IsChecked == true
                ? RemoteVisibility.Internal
                : RemoteVisibility.Private;
        Options = new RemotePublishOptions(
            repositoryName,
            remoteName,
            DescriptionText.Text,
            visibility,
            PushOption.IsChecked == true);
        DialogResult = true;
    }
}
