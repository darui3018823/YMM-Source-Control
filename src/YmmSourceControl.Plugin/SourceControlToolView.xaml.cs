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
        await CommitAsync(amend: false, CommitFollowUp.None);
    }

    private async void PrimaryActionClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.IsPublishRemotePrimaryAction == true)
        {
            await PublishRemoteAsync();
        }
        else
        {
            await CommitAsync(amend: false, CommitFollowUp.None);
        }
    }

    private async void AmendCommitClick(object sender, RoutedEventArgs e)
    {
        await CommitAsync(amend: true, CommitFollowUp.None);
    }

    private async void CommitAndPushClick(object sender, RoutedEventArgs e)
    {
        await CommitAsync(amend: false, CommitFollowUp.Push);
    }

    private void OpenCommitMenuClick(object sender, RoutedEventArgs e)
    {
        OpenButtonMenu(sender);
    }

    private async Task CommitAsync(bool amend, CommitFollowUp followUp)
    {
        if (ViewModel is null)
        {
            return;
        }

        var committed = false;
        await ViewModel.RunCommitAsync(async () =>
        {
            await ExecuteAsync(async () =>
            {
                if (!await PrepareCommitAsync(amend))
                {
                    return;
                }

                if (amend)
                {
                    await ViewModel.AmendCommitAsync(CommitMessage.Text);
                }
                else
                {
                    await ViewModel.CommitAsync(CommitMessage.Text);
                }

                committed = true;
                if (followUp == CommitFollowUp.Push)
                {
                    await PushWithRecoveryAsync();
                }
            });
        });

        if (committed)
        {
            CommitMessage.Clear();
        }
    }

    private async Task<bool> PrepareCommitAsync(bool amend)
    {
        if (ViewModel is null || ViewModel.Staged.Count > 0)
        {
            return ViewModel is not null;
        }

        if (amend && ViewModel.Changes.Count == 0)
        {
            return true;
        }

        if (ViewModel.Changes.Count == 0)
        {
            throw new InvalidOperationException("コミットする変更がありません。");
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
                    throw new InvalidOperationException("ステージ済みの変更がありません。先に変更をステージしてください。");
                }

                return false;
            }

            shouldStageAll = true;
            if (dialog.Choice == SmartCommitPreference.Always)
            {
                ViewModel.SetSmartCommitPreference(SmartCommitPreference.Always);
            }
        }

        if (ViewModel.SmartCommitPreference == SmartCommitPreference.Never)
        {
            throw new InvalidOperationException("ステージ済みの変更がありません。先に変更をステージしてください。");
        }

        if (shouldStageAll)
        {
            await ViewModel.StageAllAsync();
        }

        return true;
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

    private async void FetchClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ExecuteAsync(() => ViewModel.RunFetchAsync(ViewModel.FetchAsync));
        }
    }

    private async void PullClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var strategy = ChoosePullStrategy("Remoteの変更を現在のブランチへ取り込みます。");
        if (strategy is not null)
        {
            await ExecuteAsync(() => ViewModel.RunPullAsync(() => ViewModel.PullAsync(strategy.Value)));
        }
    }

    private async void PushClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ExecuteAsync(PushWithRecoveryAsync);
        }
    }

    private async void PublishRemoteClick(object sender, RoutedEventArgs e)
    {
        await PublishRemoteAsync();
    }

    private async Task PublishRemoteAsync()
    {
        if (ViewModel?.RepositoryRoot is not { } repositoryRoot)
        {
            return;
        }

        var repositoryName = Path.GetFileName(repositoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var dialog = new RemotePublishDialog(repositoryName)
        {
            Owner = Window.GetWindow(this),
        };
        if (dialog.ShowDialog() == true && dialog.Options is not null)
        {
            await ExecuteAsync(() => ViewModel.PublishRemoteAsync(dialog.Options));
        }
    }

    private async void MergeBranchClick(object sender, RoutedEventArgs e)
    {
        await RunBranchOperationAsync(PullStrategy.Merge);
    }

    private async void RebaseBranchClick(object sender, RoutedEventArgs e)
    {
        await RunBranchOperationAsync(PullStrategy.Rebase);
    }

    private void OpenRepositoryMenuClick(object sender, RoutedEventArgs e)
    {
        OpenButtonMenu(sender);
    }

    private async Task RunBranchOperationAsync(PullStrategy strategy)
    {
        if (ViewModel is null)
        {
            return;
        }

        IReadOnlyList<string> branches;
        try
        {
            branches = await ViewModel.GetBranchTargetsAsync();
        }
        catch (GitCommandException exception)
        {
            ViewModel.SetError(exception);
            return;
        }

        var isRebase = strategy == PullStrategy.Rebase;
        var dialog = new BranchOperationDialog(
            isRebase ? "ブランチをRebase" : "ブランチをMerge",
            isRebase ? "現在のブランチをRebase" : "現在のブランチへMerge",
            isRebase
                ? "現在のブランチのコミットを、選択したブランチの先頭へ載せ直します。"
                : "選択したブランチの変更を、現在のブランチへ統合します。",
            isRebase ? "Rebase" : "Merge",
            branches)
        {
            Owner = Window.GetWindow(this),
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await ExecuteAsync(() => isRebase
            ? ViewModel.RebaseAsync(dialog.TargetBranch)
            : ViewModel.MergeAsync(dialog.TargetBranch));
    }

    private async void KeepLocalConflictClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null && (sender as Button)?.Tag is GitFileStatus file)
        {
            await ExecuteAsync(() => ViewModel.ResolveConflictAsync(file, ConflictResolution.KeepLocal));
        }
    }

    private async void KeepIncomingConflictClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null && (sender as Button)?.Tag is GitFileStatus file)
        {
            await ExecuteAsync(() => ViewModel.ResolveConflictAsync(file, ConflictResolution.KeepIncoming));
        }
    }

    private async void ContinueOperationClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ExecuteAsync(ViewModel.ContinueOperationAsync);
        }
    }

    private async void AbortOperationClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var result = MessageBox.Show(
            $"進行中の{ViewModel.OperationTitle.Replace("を解決中", string.Empty, StringComparison.Ordinal)}を中止し、開始前の状態へ戻しますか？",
            "Git操作を中止",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        if (result == MessageBoxResult.OK)
        {
            await ExecuteAsync(ViewModel.AbortOperationAsync);
        }
    }

    private PullStrategy? ChoosePullStrategy(string reason)
    {
        if (ViewModel is null)
        {
            return null;
        }

        var savedStrategy = ViewModel.PullStrategyPreference.ToStrategy();
        if (savedStrategy is not null)
        {
            return savedStrategy;
        }

        var dialog = new PullStrategyDialog(reason)
        {
            Owner = Window.GetWindow(this),
        };
        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        if (dialog.ShouldRemember)
        {
            ViewModel.SetPullStrategyPreference(dialog.Strategy.ToPreference());
        }

        return dialog.Strategy;
    }

    private async Task PushWithRecoveryAsync()
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.RunPushAsync(async () =>
        {
            try
            {
                await ViewModel.PushAsync();
            }
            catch (GitPushRejectedException)
            {
                var strategy = ChoosePullStrategy(
                    "Remoteに未取得の変更があるためPushが拒否されました。取り込み方法を選択すると、Pull後にPushを再試行します。");
                if (strategy is null)
                {
                    return;
                }

                await ViewModel.PullAsync(strategy.Value);
                await ViewModel.PushAsync();
            }
        });
    }

    private static void OpenButtonMenu(object sender)
    {
        if (sender is Button { ContextMenu: not null } button)
        {
            var menu = button.ContextMenu;
            button.Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                () =>
                {
                    menu.PlacementTarget = button;
                    menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                    menu.IsOpen = true;
                });
        }
    }

    private async void FileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isInitializing)
        {
            return;
        }

        var file = TakeSelection<GitFileStatus>(sender);
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
        var commit = TakeSelection<GitCommit>(sender);
        if (ViewModel is null || commit is null)
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

    private static T? TakeSelection<T>(object sender)
        where T : class
    {
        if (sender is not ListBox listBox || listBox.SelectedItem is not T item)
        {
            return null;
        }

        listBox.SelectedItem = null;
        return item;
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

        Exception? failure = null;
        try
        {
            await operation();
        }
        catch (GitCommandException exception)
        {
            failure = exception;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            failure = exception;
        }

        await RefreshAsync();
        if (failure is GitCommandException gitException)
        {
            ViewModel.SetError(gitException);
        }
        else if (failure is not null)
        {
            ViewModel.SetError(failure.Message);
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

    private enum CommitFollowUp
    {
        None,
        Push,
    }
}
