using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using YmmSourceControl.Core.Models;

namespace YmmSourceControl.Core.Git;

public sealed class GitClient(string workingDirectory) : IGitClient
{
    private readonly string workingDirectory = Path.GetFullPath(workingDirectory);

    public async Task<RepositorySnapshot?> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var rootResult = await RunAsync(
            workingDirectory,
            ["rev-parse", "--show-toplevel"],
            cancellationToken,
            allowFailure: true);

        if (rootResult.ExitCode != 0)
        {
            return null;
        }

        var root = rootResult.StandardOutput.Trim();
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        var branchResult = await RunAsync(root, ["branch", "--show-current"], cancellationToken);
        var statusResult = await RunAsync(
            root,
            ["status", "--porcelain=v1", "-z", "--untracked-files=all"],
            cancellationToken);

        return new RepositorySnapshot(
            root,
            branchResult.StandardOutput.Trim() is { Length: > 0 } branch ? branch : "(detached HEAD)",
            ParseStatus(statusResult.StandardOutput));
    }

    public async Task<string> GetDiffAsync(
        string path,
        bool staged,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken)
            ?? throw new GitCommandException("Gitリポジトリが見つかりません。", 128, string.Empty);

        IReadOnlyList<string> arguments = staged
            ? ["diff", "--cached", "--no-ext-diff", "--", path]
            : ["diff", "--no-ext-diff", "--", path];
        var result = await RunAsync(snapshot.RootPath, arguments, cancellationToken);
        return result.StandardOutput;
    }

    public async Task StageAsync(string path, CancellationToken cancellationToken = default)
    {
        var root = await RequireRootAsync(cancellationToken);
        await RunAsync(root, ["add", "--", path], cancellationToken);
    }

    public async Task UnstageAsync(string path, CancellationToken cancellationToken = default)
    {
        var root = await RequireRootAsync(cancellationToken);
        await RunAsync(root, ["reset", "--", path], cancellationToken);
    }

    public async Task DiscardAsync(string path, CancellationToken cancellationToken = default)
    {
        var root = await RequireRootAsync(cancellationToken);
        var snapshot = await GetSnapshotAsync(cancellationToken)
            ?? throw new GitCommandException("Gitリポジトリが見つかりません。", 128, string.Empty);
        var file = snapshot.Files.FirstOrDefault(x => x.Path == path);

        if (file is null)
        {
            return;
        }

        if (file.IsUntracked)
        {
            File.Delete(Path.Combine(root, path));
            return;
        }

        await RunAsync(root, ["restore", "--worktree", "--", path], cancellationToken);
    }

    public async Task CommitAsync(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("コミットメッセージを入力してください。", nameof(message));
        }

        var root = await RequireRootAsync(cancellationToken);
        await RunAsync(root, ["commit", "-m", message], cancellationToken);
    }

    private async Task<string> RequireRootAsync(CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken)
            ?? throw new GitCommandException("Gitリポジトリが見つかりません。", 128, string.Empty);
        return snapshot.RootPath;
    }

    private static IReadOnlyList<GitFileStatus> ParseStatus(string output)
    {
        var records = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<GitFileStatus>(records.Length);

        for (var index = 0; index < records.Length; index++)
        {
            var record = records[index];
            if (record.Length < 3)
            {
                continue;
            }

            var indexCode = record[0].ToString();
            var worktreeCode = record[1].ToString();
            var path = record[3..];

            if (indexCode is "R" or "C" && index + 1 < records.Length)
            {
                index++;
            }

            result.Add(new GitFileStatus(
                path,
                indexCode,
                worktreeCode,
                GetFileKind(indexCode, worktreeCode)));
        }

        return result;
    }

    private static GitFileKind GetFileKind(string indexCode, string worktreeCode)
    {
        if (indexCode == "?" && worktreeCode == "?")
        {
            return GitFileKind.Untracked;
        }

        if (indexCode == "U" || worktreeCode == "U" || indexCode == "A" && worktreeCode == "A")
        {
            return GitFileKind.Conflicted;
        }

        var code = indexCode is not " " ? indexCode : worktreeCode;
        return code switch
        {
            "M" => GitFileKind.Modified,
            "A" => GitFileKind.Added,
            "D" => GitFileKind.Deleted,
            "R" => GitFileKind.Renamed,
            _ => GitFileKind.Unknown,
        };
    }

    private static async Task<GitResult> RunAsync(
        string directory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        bool allowFailure = false)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = directory,
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

        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            throw new GitCommandException("git コマンドを起動できません。Git がインストールされているか確認してください。", -1, exception.Message);
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var result = new GitResult(
            process.ExitCode,
            await standardOutputTask,
            await standardErrorTask);

        if (!allowFailure && result.ExitCode != 0)
        {
            throw new GitCommandException(
                $"git {arguments[0]} に失敗しました。",
                result.ExitCode,
                result.StandardError.Trim());
        }

        return result;
    }

    private sealed record GitResult(int ExitCode, string StandardOutput, string StandardError);
}
