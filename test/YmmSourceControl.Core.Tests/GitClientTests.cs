using System.Diagnostics;
using System.Text;
using YmmSourceControl.Core.Git;
using YmmSourceControl.Core.Models;

namespace YmmSourceControl.Core.Tests;

public sealed class GitClientTests
{
    [Fact]
    public async Task Snapshot_is_null_outside_a_git_repository()
    {
        using var repository = TemporaryDirectory.Create();
        var client = new GitClient(repository.Path);

        Assert.Null(await client.GetSnapshotAsync());
    }

    [Fact]
    public async Task Stage_commit_history_and_commit_files_are_reported()
    {
        using var repository = TemporaryDirectory.Create();
        var client = new GitClient(repository.Path);
        await client.InitializeRepositoryAsync();
        await ConfigureIdentityAsync(repository.Path);
        await File.WriteAllTextAsync(Path.Combine(repository.Path, "notes.txt"), "first line\n");

        var beforeStage = await client.GetSnapshotAsync();
        Assert.NotNull(beforeStage);
        Assert.Collection(beforeStage!.Files,
            file =>
            {
                Assert.Equal("notes.txt", file.Path);
                Assert.Equal(GitFileKind.Untracked, file.Kind);
            });

        await client.StageAsync("notes.txt");
        var staged = await client.GetSnapshotAsync();
        Assert.NotNull(staged);
        Assert.Single(staged!.Staged);
        Assert.Empty(staged.Changes);

        await client.CommitAsync("Add notes");

        var clean = await client.GetSnapshotAsync();
        Assert.NotNull(clean);
        Assert.Empty(clean!.Files);

        var history = await client.GetHistoryAsync();
        var commit = Assert.Single(history);
        Assert.Equal("Add notes", commit.Subject);
        Assert.NotEmpty(commit.ShortHash);

        var files = await client.GetCommitFilesAsync(commit.ShortHash);
        var commitFile = Assert.Single(files);
        Assert.Equal("A", commitFile.Status);
        Assert.Equal("notes.txt", commitFile.Path);

        var changes = await client.GetCommitChangesAsync(commit.ShortHash);
        var change = Assert.Single(changes);
        Assert.Equal("ファイルが追加されました", change.Description);
    }

    [Fact]
    public async Task Diff_and_ymmp_changes_describe_worktree_edits()
    {
        using var repository = TemporaryDirectory.Create();
        var client = new GitClient(repository.Path);
        await client.InitializeRepositoryAsync();
        await ConfigureIdentityAsync(repository.Path);
        var projectPath = Path.Combine(repository.Path, "project.ymmp");
        await File.WriteAllTextAsync(projectPath, "{\"Name\":\"Before\"}");
        await client.StageAsync("project.ymmp");
        await client.CommitAsync("Add project");

        await File.WriteAllTextAsync(projectPath, "{\"Name\":\"After\"}");

        var diff = await client.GetDiffAsync("project.ymmp", staged: false);
        Assert.Contains("Before", diff, StringComparison.Ordinal);
        Assert.Contains("After", diff, StringComparison.Ordinal);

        var changes = await client.GetChangesAsync("project.ymmp", staged: false);
        var change = Assert.Single(changes);
        Assert.Equal("名前：「Before」 → 「After」", change.Description);
        Assert.Equal("Before", change.Before);
        Assert.Equal("After", change.After);
    }

    [Fact]
    public async Task Discard_restores_tracked_files_and_removes_untracked_files()
    {
        using var repository = TemporaryDirectory.Create();
        var client = new GitClient(repository.Path);
        await client.InitializeRepositoryAsync();
        await ConfigureIdentityAsync(repository.Path);
        var trackedPath = Path.Combine(repository.Path, "tracked.txt");
        await File.WriteAllTextAsync(trackedPath, "original");
        await client.StageAsync("tracked.txt");
        await client.CommitAsync("Add tracked file");
        await File.WriteAllTextAsync(trackedPath, "changed");
        var untrackedPath = Path.Combine(repository.Path, "temporary.txt");
        await File.WriteAllTextAsync(untrackedPath, "temporary");

        await client.DiscardAsync("tracked.txt");
        await client.DiscardAsync("temporary.txt");

        Assert.Equal("original", await File.ReadAllTextAsync(trackedPath));
        Assert.False(File.Exists(untrackedPath));
    }

    [Fact]
    public async Task Invalid_branch_targets_are_rejected_before_git_runs()
    {
        using var repository = TemporaryDirectory.Create();
        var client = new GitClient(repository.Path);

        await Assert.ThrowsAsync<ArgumentException>(() => client.MergeAsync("-bad"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.RebaseAsync(" "));
    }

    private static async Task ConfigureIdentityAsync(string path)
    {
        await RunGitAsync(path, ["config", "user.name", "YMM Source Control Tests"]);
        await RunGitAsync(path, ["config", "user.email", "tests@example.invalid"]);
    }

    private static async Task RunGitAsync(string workingDirectory, IReadOnlyList<string> arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            },
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start());
        await process.WaitForExitAsync();
        var error = await process.StandardError.ReadToEndAsync();
        Assert.True(process.ExitCode == 0, error);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ymm-source-control-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                foreach (var directory in Directory.EnumerateDirectories(Path, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(directory, FileAttributes.Normal);
                }

                File.SetAttributes(Path, FileAttributes.Normal);
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
