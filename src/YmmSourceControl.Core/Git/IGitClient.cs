using YmmSourceControl.Core.Models;

namespace YmmSourceControl.Core.Git;

public interface IGitClient
{
    Task<RepositorySnapshot?> GetSnapshotAsync(CancellationToken cancellationToken = default);

    Task<string> GetDiffAsync(string path, bool staged, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GitCommitChange>> GetChangesAsync(string path, bool staged, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GitCommit>> GetHistoryAsync(int limit = 30, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GitCommitFile>> GetCommitFilesAsync(string commit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GitCommitChange>> GetCommitChangesAsync(string commit, CancellationToken cancellationToken = default);

    Task<string> GetCommitDiffAsync(string commit, CancellationToken cancellationToken = default);

    Task InitializeRepositoryAsync(CancellationToken cancellationToken = default);

    Task StageAsync(string path, CancellationToken cancellationToken = default);

    Task StageAllAsync(CancellationToken cancellationToken = default);

    Task UnstageAsync(string path, CancellationToken cancellationToken = default);

    Task DiscardAsync(string path, CancellationToken cancellationToken = default);

    Task CommitAsync(string message, CancellationToken cancellationToken = default);

    Task AmendCommitAsync(string message, CancellationToken cancellationToken = default);

    Task FetchAsync(CancellationToken cancellationToken = default);

    Task PullAsync(PullStrategy strategy, CancellationToken cancellationToken = default);

    Task PushAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetBranchTargetsAsync(CancellationToken cancellationToken = default);

    Task MergeAsync(string targetBranch, CancellationToken cancellationToken = default);

    Task RebaseAsync(string targetBranch, CancellationToken cancellationToken = default);

    Task ResolveConflictAsync(
        string path,
        ConflictResolution resolution,
        GitOperationState operationState,
        CancellationToken cancellationToken = default);

    Task<string> GetConflictContentAsync(string path, CancellationToken cancellationToken = default);

    Task ResolveConflictAsync(string path, string content, CancellationToken cancellationToken = default);

    Task ContinueOperationAsync(GitOperationState operationState, CancellationToken cancellationToken = default);

    Task AbortOperationAsync(GitOperationState operationState, CancellationToken cancellationToken = default);

    Task PublishRemoteAsync(RemotePublishOptions options, CancellationToken cancellationToken = default);
}
