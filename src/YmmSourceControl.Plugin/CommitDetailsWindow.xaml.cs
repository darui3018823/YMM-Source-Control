using System.Windows;
using System.Windows.Controls;
using YmmSourceControl.Core.Models;

namespace YmmSourceControl.Plugin;

public partial class CommitDetailsWindow : Window
{
    public CommitDetailsWindow(GitCommit commit, IReadOnlyList<GitCommitFile> files, string diff)
    {
        InitializeComponent();
        SubjectText.Text = commit.Subject;
        HashText.Text = commit.ShortHash;
        AuthorText.Text = commit.Author;
        DateText.Text = commit.Date;
        FileCountText.Text = $"{files.Count} ファイル";
        DiffText.Text = diff;
        ChangedFilesList.ItemsSource = files;
        ChangedFilesList.ItemTemplate = CreateFileTemplate();
    }

    private static DataTemplate CreateFileTemplate()
    {
        var template = new DataTemplate(typeof(GitCommitFile));
        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        panel.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 1, 0, 1));
        panel.AppendChild(CreateTextBlock("Status", 42, "#2B6CB0"));
        panel.AppendChild(CreateTextBlock("FileName", 0, "#333333"));
        panel.AppendChild(CreateTextBlock("DirectoryName", 0, "#777777", 10));
        template.VisualTree = panel;
        return template;
    }

    private static FrameworkElementFactory CreateTextBlock(
        string bindingPath,
        double width,
        string foreground,
        double fontSize = 12)
    {
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(bindingPath));
        text.SetValue(TextBlock.ForegroundProperty, new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(foreground)));
        text.SetValue(TextBlock.FontSizeProperty, fontSize);
        if (width > 0)
        {
            text.SetValue(FrameworkElement.WidthProperty, width);
        }

        return text;
    }
}
