using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using YmmSourceControl.Core.Git;
using YmmSourceControl.Core.Models;

namespace YmmSourceControl.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel;
    private readonly DispatcherTimer refreshTimer;
    private FileSystemWatcher? watcher;

    public MainWindow()
    {
        InitializeComponent();
        viewModel = new MainWindowViewModel(new GitClient(Environment.CurrentDirectory));
        DataContext = viewModel;
        refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        refreshTimer.Tick += RefreshTimerTick;
        Loaded += LoadedAsync;
        Closed += ClosedHandler;
    }

    private async void LoadedAsync(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async void RefreshClick(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async void CommitClick(object sender, RoutedEventArgs e)
    {
        await ExecuteAsync(() => viewModel.CommitAsync(CommitMessage.Text));
        CommitMessage.Clear();
    }

    private async void StageClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is GitFileStatus file)
        {
            await ExecuteAsync(() => viewModel.StageAsync(file.Path));
        }
    }

    private async void UnstageClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is GitFileStatus file)
        {
            await ExecuteAsync(() => viewModel.UnstageAsync(file.Path));
        }
    }

    private async void DiscardClick(object sender, RoutedEventArgs e)
    {
        var file = SelectedFile;
        if (file is null)
        {
            return;
        }

        var result = MessageBox.Show(
            $"「{file.Path}」の作業ツリーの変更を破棄しますか？",
            "Discard changes",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        if (result == MessageBoxResult.OK)
        {
            await ExecuteAsync(() => viewModel.DiscardAsync(file.Path));
        }
    }

    private async void FileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedFile is null)
        {
            DiffPreview.Clear();
            return;
        }

        try
        {
            DiffPreview.Text = await viewModel.GetDiffAsync(SelectedFile);
        }
        catch (GitCommandException exception)
        {
            DiffPreview.Text = exception.StandardError;
        }
    }

    private GitFileStatus? SelectedFile => ChangesList.SelectedItem as GitFileStatus ?? StagedList.SelectedItem as GitFileStatus;

    private async void RefreshTimerTick(object? sender, EventArgs e)
    {
        refreshTimer.Stop();
        await RefreshAsync();
    }

    private void ScheduleRefresh(object? sender, FileSystemEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            refreshTimer.Stop();
            refreshTimer.Start();
        });
    }

    private async Task RefreshAsync()
    {
        await viewModel.RefreshAsync();
        ConfigureWatcher(viewModel.RepositoryRoot);
    }

    private async Task ExecuteAsync(Func<Task> operation)
    {
        try
        {
            await operation();
            await RefreshAsync();
        }
        catch (GitCommandException exception)
        {
            viewModel.ErrorMessage = string.IsNullOrWhiteSpace(exception.StandardError)
                ? exception.Message
                : exception.StandardError;
        }
        catch (ArgumentException exception)
        {
            viewModel.ErrorMessage = exception.Message;
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
        watcher.Changed += ScheduleRefresh;
        watcher.Created += ScheduleRefresh;
        watcher.Deleted += ScheduleRefresh;
        watcher.Renamed += (sender, args) => ScheduleRefresh(sender, args);
    }

    private void ClosedHandler(object? sender, EventArgs e)
    {
        watcher?.Dispose();
        refreshTimer.Stop();
    }
}

public sealed class MainWindowViewModel(IGitClient gitClient) : INotifyPropertyChanged
{
    public ObservableCollection<GitFileStatus> Changes { get; } = [];

    public ObservableCollection<GitFileStatus> Staged { get; } = [];

    public string Branch { get; private set; } = "No repository";

    public string RepositoryState { get; private set; } = string.Empty;

    public string? RepositoryRoot { get; private set; }

    private string errorMessage = string.Empty;

    public string ErrorMessage
    {
        get => errorMessage;
        set
        {
            if (errorMessage == value)
            {
                return;
            }

            errorMessage = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task RefreshAsync()
    {
        var snapshot = await gitClient.GetSnapshotAsync();
        Changes.Clear();
        Staged.Clear();

        if (snapshot is null)
        {
            RepositoryRoot = null;
            Branch = "No repository";
            RepositoryState = "Open a Git repo";
            ErrorMessage = string.Empty;
            OnPropertyChanged(nameof(Branch));
            OnPropertyChanged(nameof(RepositoryState));
            OnPropertyChanged(nameof(RepositoryRoot));
            OnPropertyChanged(nameof(ErrorMessage));
            return;
        }

        RepositoryRoot = snapshot.RootPath;
        Branch = snapshot.Branch;
        RepositoryState = snapshot.Files.Count == 0 ? "Clean" : $"{snapshot.Files.Count} pending";
        foreach (var file in snapshot.Changes)
        {
            Changes.Add(file);
        }

        foreach (var file in snapshot.Staged)
        {
            Staged.Add(file);
        }

        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(Branch));
        OnPropertyChanged(nameof(RepositoryState));
        OnPropertyChanged(nameof(RepositoryRoot));
        OnPropertyChanged(nameof(ErrorMessage));
    }

    public Task<string> GetDiffAsync(GitFileStatus file) => gitClient.GetDiffAsync(file.Path, file.IsStaged);

    public Task StageAsync(string path) => gitClient.StageAsync(path);

    public Task UnstageAsync(string path) => gitClient.UnstageAsync(path);

    public Task DiscardAsync(string path) => gitClient.DiscardAsync(path);

    public Task CommitAsync(string message) => gitClient.CommitAsync(message);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
