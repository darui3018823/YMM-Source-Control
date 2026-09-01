using YmmSourceControl.Core.Models;

namespace YmmSourceControl.Core.Tests;

public sealed class RepositorySnapshotTests
{
    [Fact]
    public void Snapshot_groups_files_for_the_three_source_control_lists()
    {
        var files = new[]
        {
            new GitFileStatus("modified.txt", " ", "M", GitFileKind.Modified),
            new GitFileStatus("staged.txt", "M", " ", GitFileKind.Modified),
            new GitFileStatus("untracked.txt", "?", "?", GitFileKind.Untracked),
            new GitFileStatus("conflict.txt", "U", "U", GitFileKind.Conflicted),
        };
        var snapshot = new RepositorySnapshot(
            "C:\\repo",
            "main",
            files,
            ["origin"],
            "origin/main",
            2,
            1,
            GitOperationState.None);

        Assert.Equal(["modified.txt", "untracked.txt"], snapshot.Changes.Select(file => file.Path));
        Assert.Equal(["staged.txt"], snapshot.Staged.Select(file => file.Path));
        Assert.Equal(["conflict.txt"], snapshot.Conflicts.Select(file => file.Path));
        Assert.True(snapshot.HasRemote);
    }
}
