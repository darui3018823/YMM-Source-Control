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
}
