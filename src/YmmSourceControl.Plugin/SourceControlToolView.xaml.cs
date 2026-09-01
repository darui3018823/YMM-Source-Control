using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using YmmSourceControl.Core.Git;
using YmmSourceControl.Core.Models;

namespace YmmSourceControl.Plugin;

public partial class SourceControlToolView : UserControl
{
    private readonly DispatcherTimer refreshTimer;
    private FileSystemWatcher? watcher;
    private string? watchedRoot;
    private FileDiffWindow? diffWindow;
    private bool isInitializing;

    public SourceControlToolView()
    {
        InitializeComponent();
        refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        refreshTimer.Tick += RefreshTimerTick;
        Loaded += LoadedAsync;
        Unloaded += UnloadedHandler;
    }

    private SourceControlToolViewModel? ViewModel => DataContext as SourceControlToolViewModel;

    private async void LoadedAsync(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        isInitializing = true;
        try
        {
            await ViewModel.RefreshAsync();
            ChangesList.SelectedItem = null;
            StagedList.SelectedItem = null;
            ConfigureWatcher(ViewModel.RepositoryRoot);
        }
        finally
        {
            isInitializing = false;
        }
    }

    private async void RefreshClick(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async void ChooseRepositoryClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Git repository を選択",
        };
        if (dialog.ShowDialog() != true || ViewModel is null)
        {
            return;
        }

        ViewModel.SetRepository(dialog.FolderName);
        await RefreshAsync();
    }

    private async void InitializeRepositoryClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var result = MessageBox.Show(
            $"このフォルダーにGitリポジトリを初期化しますか？\n\n{ViewModel.SelectedDirectory}",
            "Gitリポジトリを初期化",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.OK)
        {
            return;
        }

        try
        {
            await ViewModel.InitializeRepositoryAsync();
            await RefreshAsync();
        }
        catch (GitCommandException exception)
        {
            ViewModel.SetError(exception);
        }
        catch (DirectoryNotFoundException exception)
        {
            ViewModel.SetError(exception.Message);
        }
    }

    private async void CommitClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        try
        {
            if (ViewModel.Staged.Count == 0)
            {
                if (ViewModel.Changes.Count == 0)
                {
                    ViewModel.SetError("コミットする変更がありません。");
                    return;
                }

                var shouldStageAll = ViewModel.SmartCommitPreference == SmartCommitPreference.Always;
                if (ViewModel.SmartCommitPreference == SmartCommitPreference.Ask)
                {
                    var dialog = new SmartCommitDialog
                    {
                        Owner = Window.GetWindow(this),
                    };
                    if (dialog.ShowDialog() != true)
                    {
                        if (dialog.Choice == SmartCommitPreference.Never)
                        {
                            ViewModel.SetSmartCommitPreference(SmartCommitPreference.Never);
                            ViewModel.SetError("ステージ済みの変更がありません。先に変更をステージしてください。");
                        }

                        return;
                    }

                    shouldStageAll = true;
                    if (dialog.Choice == SmartCommitPreference.Always)
                    {
                        ViewModel.SetSmartCommitPreference(SmartCommitPreference.Always);
                    }
                }

                if (ViewModel.SmartCommitPreference == SmartCommitPreference.Never)
                {
                    ViewModel.SetError("ステージ済みの変更がありません。先に変更をステージしてください。");
                    return;
                }

                if (shouldStageAll)
                {
                    await ViewModel.StageAllAsync();
                }
            }

            await ViewModel.CommitAsync(CommitMessage.Text);
            CommitMessage.Clear();
            await RefreshAsync();
        }
        catch (GitCommandException exception)
        {
            ViewModel.SetError(exception);
        }
        catch (ArgumentException exception)
        {
            ViewModel.SetError(exception.Message);
        }
    }

    private async void StageClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null && (sender as Button)?.Tag is GitFileStatus file)
        {
            await ExecuteAsync(() => ViewModel.StageAsync(file.Path));
        }
    }

    private async void UnstageClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null && (sender as Button)?.Tag is GitFileStatus file)
        {
            await ExecuteAsync(() => ViewModel.UnstageAsync(file.Path));
        }
    }

    private async void DiscardClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || (sender as Button)?.Tag is not GitFileStatus file)
        {
            return;
        }

        var dialog = new DiscardChangesDialog(file.Path)
        {
            Owner = Window.GetWindow(this),
        };
        if (dialog.ShowDialog() == true)
        {
            await ExecuteAsync(() => ViewModel.DiscardAsync(file.Path));
        }
    }

    private async void FileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isInitializing)
        {
            return;
        }

        var file = ChangesList.SelectedItem as GitFileStatus ?? StagedList.SelectedItem as GitFileStatus;
        if (file is null || ViewModel is null)
        {
            return;
        }

        try
        {
            var changes = await ViewModel.GetChangesAsync(file);
            ViewModel.DiffText = await ViewModel.GetDiffAsync(file);
            ShowDiffWindow(file, changes, ViewModel.DiffText);
        }
        catch (GitCommandException exception)
        {
            ViewModel.DiffText = exception.StandardError;
        }
    }

    private void ShowDiffWindow(GitFileStatus file, IReadOnlyList<GitCommitChange> changes, string diff)
    {
        if (diffWindow is null || !diffWindow.IsLoaded)
        {
            diffWindow = new FileDiffWindow
            {
                Owner = Window.GetWindow(this),
            };
            diffWindow.Closed += (_, _) => diffWindow = null;
        }

        diffWindow.SetDiff(file, changes, diff);
        if (!diffWindow.IsVisible)
        {
            diffWindow.Show();
        }
        else
        {
            diffWindow.Activate();
        }
    }

    private async void CommitSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null || (sender as ListBox)?.SelectedItem is not GitCommit commit)
        {
            return;
        }

        try
        {
            var files = await ViewModel.GetCommitFilesAsync(commit);
            var changes = await ViewModel.GetCommitChangesAsync(commit);
            var diff = await ViewModel.GetCommitDiffAsync(commit);
            var detailsWindow = new CommitDetailsWindow(commit, files, changes, diff)
            {
                Owner = Window.GetWindow(this),
            };
            ViewModel.SetError(string.Empty);
            detailsWindow.Show();
        }
        catch (GitCommandException exception)
        {
            ViewModel.SetError(exception);
        }
    }

    private async void RefreshTimerTick(object? sender, EventArgs e)
    {
        refreshTimer.Stop();
        await RefreshAsync();
    }

    private void ScheduleRefresh(object? sender, FileSystemEventArgs e)
    {
        if (watchedRoot is not null && IsGitMetadataPath(watchedRoot, e.FullPath))
        {
            return;
        }

        Dispatcher.Invoke(() =>
        {
            refreshTimer.Stop();
            refreshTimer.Start();
        });
    }

    private async Task RefreshAsync()
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.RefreshAsync();
        ConfigureWatcher(ViewModel.RepositoryRoot);
    }

    private async Task ExecuteAsync(Func<Task> operation)
    {
        if (ViewModel is null)
        {
            return;
        }

        try
        {
            await operation();
            await RefreshAsync();
        }
        catch (GitCommandException exception)
        {
            ViewModel.SetError(exception);
        }
    }

    private void ConfigureWatcher(string? root)
    {
        if (string.Equals(watcher?.Path, root, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        watcher?.Dispose();
        watcher = null;
        watchedRoot = null;
        if (root is null || !Directory.Exists(root))
        {
            return;
        }

        watcher = new FileSystemWatcher(root)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            EnableRaisingEvents = true,
        };
        watchedRoot = root;
        watcher.Changed += ScheduleRefresh;
        watcher.Created += ScheduleRefresh;
        watcher.Deleted += ScheduleRefresh;
        watcher.Renamed += (sender, args) => ScheduleRefresh(sender, args);
    }

    private void UnloadedHandler(object sender, RoutedEventArgs e)
    {
        watcher?.Dispose();
        refreshTimer.Stop();
        diffWindow?.Close();
        diffWindow = null;
    }

    private static bool IsGitMetadataPath(string root, string path)
    {
        var relativePath = Path.GetRelativePath(root, path);
        return relativePath.Equals(".git", StringComparison.OrdinalIgnoreCase)
            || relativePath.StartsWith($".git{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || relativePath.StartsWith($".git{Path.AltDirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }
}
