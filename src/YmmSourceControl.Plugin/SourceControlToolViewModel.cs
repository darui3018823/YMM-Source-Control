using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
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
    private SmartCommitPreference smartCommitPreference = SmartCommitPreference.Ask;
    private PullStrategyPreference pullStrategyPreference = PullStrategyPreference.Ask;
    private bool hasRemote;
    private string remoteSummary = "Remoteなし";
    private int ahead;
    private int behind;
    private GitOperationState operationState;
    private bool isPublishingRemote;
    private bool isCommitting;
    private bool isFetching;
    private bool isPulling;
    private bool isPushing;

    public SourceControlToolViewModel()
    {
        gitClient = new GitClient(workingDirectory);
    }

    public ObservableCollection<GitFileStatus> Changes { get; } = [];

    public ObservableCollection<GitFileStatus> Staged { get; } = [];

    public ObservableCollection<GitFileStatus> Conflicts { get; } = [];

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
                NotifyPrimaryActionChanged();
            }
        }
    }

    public bool HasRepository => RepositoryRoot is not null;

    public bool CanInitializeRepository => repositorySelected && !HasRepository;

    public bool HasRemote
    {
        get => hasRemote;
        private set
        {
            if (SetField(ref hasRemote, value))
            {
                NotifyPrimaryActionChanged();
            }
        }
    }

    public bool IsPublishRemotePrimaryAction => HasRepository
        && !HasRemote
        && Changes.Count == 0
        && Staged.Count == 0
        && Conflicts.Count == 0
        && !HasActiveOperation;

    public string PrimaryActionLabel => IsPublishRemotePrimaryAction
        ? "GitHubへRemoteを発行"
        : "✓  コミット";

    public bool IsPublishingRemote
    {
        get => isPublishingRemote;
        private set => SetField(ref isPublishingRemote, value);
    }

    public bool IsCommitting
    {
        get => isCommitting;
        private set => SetField(ref isCommitting, value);
    }

    public bool IsFetching
    {
        get => isFetching;
        private set => SetField(ref isFetching, value);
    }

    public bool IsPulling
    {
        get => isPulling;
        private set => SetField(ref isPulling, value);
    }

    public bool IsPushing
    {
        get => isPushing;
        private set => SetField(ref isPushing, value);
    }

    public string RemoteSummary
    {
        get => remoteSummary;
        private set => SetField(ref remoteSummary, value);
    }

    public int Ahead
    {
        get => ahead;
        private set => SetField(ref ahead, value);
    }

    public int Behind
    {
        get => behind;
        private set => SetField(ref behind, value);
    }

    public GitOperationState OperationState
    {
        get => operationState;
        private set
        {
            if (SetField(ref operationState, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasActiveOperation)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OperationTitle)));
                NotifyPrimaryActionChanged();
            }
        }
    }

    public bool HasActiveOperation => OperationState != GitOperationState.None;

    public bool HasConflicts => Conflicts.Count > 0;

    public string OperationTitle => OperationState switch
    {
        GitOperationState.Merge => "Mergeを解決中",
        GitOperationState.Rebase => "Rebaseを解決中",
        _ => "競合を解決中",
    };

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

    public SmartCommitPreference SmartCommitPreference
    {
        get => smartCommitPreference;
        private set => smartCommitPreference = value;
    }

    public PullStrategyPreference PullStrategyPreference
    {
        get => pullStrategyPreference;
        private set => pullStrategyPreference = value;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    event EventHandler<CreateNewToolViewRequestedEventArgs>? IToolViewModel.CreateNewToolViewRequested
    {
        add { }
        remove { }
    }

    public ToolState SaveState() => new()
    {
        SavedState = JsonSerializer.Serialize(new PersistedState
        {
            WorkingDirectory = workingDirectory,
            SmartCommitPreference = SmartCommitPreference,
            PullStrategyPreference = PullStrategyPreference,
        }),
    };

    public void LoadState(ToolState state)
    {
        var savedDirectory = state.SavedState;
        if (!string.IsNullOrWhiteSpace(savedDirectory) && !Directory.Exists(savedDirectory))
        {
            try
            {
                var persistedState = JsonSerializer.Deserialize<PersistedState>(savedDirectory);
                if (persistedState is not null)
                {
                    SmartCommitPreference = persistedState.SmartCommitPreference;
                    PullStrategyPreference = persistedState.PullStrategyPreference;
                    savedDirectory = persistedState.WorkingDirectory;
                }
            }
            catch (JsonException)
            {
                // Older versions stored only the directory path.
            }
        }

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

            if (snapshot is null)
            {
                ReplaceCollection(Changes, []);
                ReplaceCollection(Staged, []);
                ReplaceCollection(Conflicts, []);
                ReplaceCollection(History, []);
                RepositoryRoot = null;
                Branch = "No repository";
                RepositoryState = "Choose a Git repo";
                HasRemote = false;
                RemoteSummary = "Remoteなし";
                Ahead = 0;
                Behind = 0;
                OperationState = GitOperationState.None;
                NotifyConflictStateChanged();
                ErrorMessage = string.Empty;
                return;
            }

            RepositoryRoot = snapshot.RootPath;
            Branch = snapshot.Branch;
            RepositoryState = snapshot.OperationState switch
            {
                GitOperationState.Merge => "Merge中",
                GitOperationState.Rebase => "Rebase中",
                _ => snapshot.Files.Count == 0 ? "Clean" : $"{snapshot.Files.Count} pending",
            };
            HasRemote = snapshot.HasRemote;
            RemoteSummary = snapshot.Upstream
                ?? (snapshot.Remotes.Count > 0 ? snapshot.Remotes[0] : "Remoteなし");
            Ahead = snapshot.Ahead;
            Behind = snapshot.Behind;
            OperationState = snapshot.OperationState;
            ReplaceCollection(Changes, snapshot.Changes);
            ReplaceCollection(Staged, snapshot.Staged);
            ReplaceCollection(Conflicts, snapshot.Conflicts);
            NotifyConflictStateChanged();
            ReplaceCollection(History, await gitClient.GetHistoryAsync(24));

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

    public Task<IReadOnlyList<GitCommitChange>> GetChangesAsync(GitFileStatus file) => gitClient.GetChangesAsync(file.Path, file.IsStaged);

    public Task<IReadOnlyList<GitCommitFile>> GetCommitFilesAsync(GitCommit commit) => gitClient.GetCommitFilesAsync(commit.ShortHash);

    public Task<IReadOnlyList<GitCommitChange>> GetCommitChangesAsync(GitCommit commit) => gitClient.GetCommitChangesAsync(commit.ShortHash);

    public Task<string> GetCommitDiffAsync(GitCommit commit) => gitClient.GetCommitDiffAsync(commit.ShortHash);

    public Task StageAsync(string path) => gitClient.StageAsync(path);

    public Task StageAllAsync() => gitClient.StageAllAsync();

    public void SetSmartCommitPreference(SmartCommitPreference preference) => SmartCommitPreference = preference;

    public void SetPullStrategyPreference(PullStrategyPreference preference) => PullStrategyPreference = preference;

    public Task UnstageAsync(string path) => gitClient.UnstageAsync(path);

    public Task DiscardAsync(string path) => gitClient.DiscardAsync(path);

    public Task CommitAsync(string message) => gitClient.CommitAsync(message);

    public Task AmendCommitAsync(string message) => gitClient.AmendCommitAsync(message);

    public async Task RunCommitAsync(Func<Task> operation)
    {
        if (IsCommitting)
        {
            return;
        }

        IsCommitting = true;
        try
        {
            await operation();
        }
        finally
        {
            IsCommitting = false;
        }
    }

    public Task FetchAsync() => gitClient.FetchAsync();

    public Task PullAsync(PullStrategy strategy) => gitClient.PullAsync(strategy);

    public Task PushAsync() => gitClient.PushAsync();

    public Task RunFetchAsync(Func<Task> operation) => RunRemoteOperationAsync(RemoteOperation.Fetch, operation);

    public Task RunPullAsync(Func<Task> operation) => RunRemoteOperationAsync(RemoteOperation.Pull, operation);

    public Task RunPushAsync(Func<Task> operation) => RunRemoteOperationAsync(RemoteOperation.Push, operation);

    public Task<IReadOnlyList<string>> GetBranchTargetsAsync() => gitClient.GetBranchTargetsAsync();

    public Task MergeAsync(string targetBranch) => gitClient.MergeAsync(targetBranch);

    public Task RebaseAsync(string targetBranch) => gitClient.RebaseAsync(targetBranch);

    public Task ResolveConflictAsync(GitFileStatus file, ConflictResolution resolution) =>
        gitClient.ResolveConflictAsync(file.Path, resolution, OperationState);

    public Task ContinueOperationAsync() => gitClient.ContinueOperationAsync(OperationState);

    public Task AbortOperationAsync() => gitClient.AbortOperationAsync(OperationState);

    public async Task PublishRemoteAsync(RemotePublishOptions options)
    {
        if (IsPublishingRemote)
        {
            return;
        }

        IsPublishingRemote = true;
        try
        {
            await gitClient.PublishRemoteAsync(options);
        }
        finally
        {
            IsPublishingRemote = false;
        }
    }

    public Task InitializeRepositoryAsync() => gitClient.InitializeRepositoryAsync();

    public void SetError(GitCommandException exception)
    {
        ErrorMessage = string.IsNullOrWhiteSpace(exception.StandardError)
            ? exception.Message
            : exception.StandardError.Trim();
    }

    public void SetError(string message) => ErrorMessage = message;

    private void NotifyConflictStateChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasConflicts)));
        NotifyPrimaryActionChanged();
    }

    private void NotifyPrimaryActionChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPublishRemotePrimaryAction)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PrimaryActionLabel)));
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        if (target.SequenceEqual(source))
        {
            return;
        }

        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

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

    private async Task RunRemoteOperationAsync(RemoteOperation operation, Func<Task> callback)
    {
        if (IsFetching || IsPulling || IsPushing)
        {
            return;
        }

        SetRemoteOperationState(operation, true);
        try
        {
            await callback();
        }
        finally
        {
            SetRemoteOperationState(operation, false);
        }
    }

    private void SetRemoteOperationState(RemoteOperation operation, bool isActive)
    {
        switch (operation)
        {
            case RemoteOperation.Fetch:
                IsFetching = isActive;
                break;
            case RemoteOperation.Pull:
                IsPulling = isActive;
                break;
            case RemoteOperation.Push:
                IsPushing = isActive;
                break;
        }
    }

    private enum RemoteOperation
    {
        Fetch,
        Pull,
        Push,
    }

    private sealed class PersistedState
    {
        public string WorkingDirectory { get; init; } = string.Empty;

        public SmartCommitPreference SmartCommitPreference { get; init; } = SmartCommitPreference.Ask;

        public PullStrategyPreference PullStrategyPreference { get; init; } = PullStrategyPreference.Ask;
    }
}
