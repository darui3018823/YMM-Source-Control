using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using YmmSourceControl.Core.Git;
using YmmSourceControl.Core.Models;
using YukkuriMovieMaker.Plugin;

namespace YmmSourceControl.Plugin;

public sealed class SourceControlToolViewModel : IToolViewModel, INotifyPropertyChanged
{
    private string workingDirectory = Environment.CurrentDirectory;
    private IGitClient gitClient;
    private string branch = "No repository";
    private string repositoryState = "Choose a Git repo";
    private string? repositoryRoot;
    private string? selectedDirectory;
    private string errorMessage = string.Empty;
    private string diffText = string.Empty;
    private bool repositorySelected;

    public SourceControlToolViewModel()
    {
        gitClient = new GitClient(workingDirectory);
    }

    public ObservableCollection<GitFileStatus> Changes { get; } = [];

    public ObservableCollection<GitFileStatus> Staged { get; } = [];

    public ObservableCollection<GitCommit> History { get; } = [];

    public string Title => "Source Control";

    public bool CanSuspend => false;

    public string Branch
    {
        get => branch;
        private set => SetField(ref branch, value);
    }

    public string RepositoryState
    {
        get => repositoryState;
        private set => SetField(ref repositoryState, value);
    }

    public string? RepositoryRoot
    {
        get => repositoryRoot;
        private set
        {
            if (SetField(ref repositoryRoot, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasRepository)));
            }
        }
    }

    public bool HasRepository => RepositoryRoot is not null;

    public bool CanInitializeRepository => repositorySelected && !HasRepository;

    public string? SelectedDirectory
    {
        get => selectedDirectory;
        private set => SetField(ref selectedDirectory, value);
    }

    public string WorkingDirectory
    {
        get => workingDirectory;
        private set => SetField(ref workingDirectory, value);
    }

    public string ErrorMessage
    {
        get => errorMessage;
        private set => SetField(ref errorMessage, value);
    }

    public string DiffText
    {
        get => diffText;
        set => SetField(ref diffText, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    event EventHandler<CreateNewToolViewRequestedEventArgs>? IToolViewModel.CreateNewToolViewRequested
    {
        add { }
        remove { }
    }

    public ToolState SaveState() => new() { SavedState = workingDirectory };

    public void LoadState(ToolState state)
    {
        var savedDirectory = state.SavedState;
        if (!string.IsNullOrWhiteSpace(savedDirectory) && Directory.Exists(savedDirectory))
        {
            SetRepository(savedDirectory);
        }
    }

    public void SetRepository(string path)
    {
        WorkingDirectory = Path.GetFullPath(path);
        SelectedDirectory = WorkingDirectory;
        repositorySelected = true;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanInitializeRepository)));
        gitClient = new GitClient(workingDirectory);
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try
        {
            var snapshot = await gitClient.GetSnapshotAsync();
            Changes.Clear();
            Staged.Clear();
            History.Clear();

            if (snapshot is null)
            {
                RepositoryRoot = null;
                Branch = "No repository";
                RepositoryState = "Choose a Git repo";
                ErrorMessage = string.Empty;
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

            foreach (var commit in await gitClient.GetHistoryAsync(24))
            {
                History.Add(commit);
            }

            ErrorMessage = string.Empty;
        }
        catch (GitCommandException exception)
        {
            ErrorMessage = string.IsNullOrWhiteSpace(exception.StandardError)
                ? exception.Message
                : exception.StandardError.Trim();
        }
    }

    public Task<string> GetDiffAsync(GitFileStatus file) => gitClient.GetDiffAsync(file.Path, file.IsStaged);

    public Task StageAsync(string path) => gitClient.StageAsync(path);

    public Task UnstageAsync(string path) => gitClient.UnstageAsync(path);

    public Task DiscardAsync(string path) => gitClient.DiscardAsync(path);

    public Task CommitAsync(string message) => gitClient.CommitAsync(message);

    public Task InitializeRepositoryAsync() => gitClient.InitializeRepositoryAsync();

    public void SetError(GitCommandException exception)
    {
        ErrorMessage = string.IsNullOrWhiteSpace(exception.StandardError)
            ? exception.Message
            : exception.StandardError.Trim();
    }

    public void SetError(string message) => ErrorMessage = message;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
