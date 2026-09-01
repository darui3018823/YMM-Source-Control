using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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

    public async Task<IReadOnlyList<GitCommit>> GetHistoryAsync(
        int limit = 30,
        CancellationToken cancellationToken = default)
    {
        var root = await RequireRootAsync(cancellationToken);
        var result = await RunAsync(
            root,
            [
                "log",
                "-n",
                Math.Max(1, limit).ToString(CultureInfo.InvariantCulture),
                "--date=short",
                "--pretty=format:%h%x00%an%x00%ad%x00%s%x00",
            ],
            cancellationToken,
            allowFailure: true);
        if (result.ExitCode != 0)
        {
            return [];
        }

        var records = result.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var commits = new List<GitCommit>();

        for (var index = 0; index + 3 < records.Length; index += 4)
        {
            commits.Add(new GitCommit(records[index], records[index + 1], records[index + 2], records[index + 3]));
        }

        return commits;
    }

    public async Task<IReadOnlyList<GitCommitFile>> GetCommitFilesAsync(
        string commit,
        CancellationToken cancellationToken = default)
    {
        var root = await RequireRootAsync(cancellationToken);
        var result = await RunAsync(
            root,
            ["-c", "core.quotePath=false", "show", "--format=", "--no-renames", "--name-status", commit],
            cancellationToken);
        var files = new List<GitCommitFile>();

        foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf('\t');
            if (separator <= 0 || separator == line.Length - 1)
            {
                continue;
            }

            files.Add(new GitCommitFile(line[..separator].Trim(), line[(separator + 1)..].TrimEnd('\r')));
        }

        return files;
    }

    public async Task<IReadOnlyList<GitCommitChange>> GetCommitChangesAsync(
        string commit,
        CancellationToken cancellationToken = default)
    {
        var root = await RequireRootAsync(cancellationToken);
        var files = await GetCommitFilesAsync(commit, cancellationToken);
        var changes = new List<GitCommitChange>();

        foreach (var file in files)
        {
            if (!file.Path.EndsWith(".ymmp", StringComparison.OrdinalIgnoreCase))
            {
                changes.Add(new GitCommitChange(file.Path, DescribeFileChange(file), null, null));
                continue;
            }

            var before = await TryGetFileContentAsync(root, $"{commit}^:{file.Path}", cancellationToken);
            var after = await TryGetFileContentAsync(root, $"{commit}:{file.Path}", cancellationToken);
            changes.AddRange(DescribeYmmpChanges(file, before, after));
        }

        return changes;
    }

    public async Task<string> GetCommitDiffAsync(
        string commit,
        CancellationToken cancellationToken = default)
    {
        var root = await RequireRootAsync(cancellationToken);
        var result = await RunAsync(
            root,
            ["-c", "core.quotePath=false", "show", "--format=fuller", "--no-ext-diff", "--no-color", commit],
            cancellationToken);
        return result.StandardOutput;
    }

    private static string DescribeFileChange(GitCommitFile file) => file.Status switch
    {
        "A" => "ファイルが追加されました",
        "D" => "ファイルが削除されました",
        "M" => "ファイルが変更されました",
        _ => "ファイルが更新されました",
    };

    private static IReadOnlyList<GitCommitChange> DescribeYmmpChanges(
        GitCommitFile file,
        string? before,
        string? after)
    {
        if (before is null && after is null)
        {
            return [new GitCommitChange(file.Path, DescribeFileChange(file), null, null)];
        }

        if (before is null)
        {
            return [new GitCommitChange(file.Path, "YMMプロジェクトが追加されました", null, null)];
        }

        if (after is null)
        {
            return [new GitCommitChange(file.Path, "YMMプロジェクトが削除されました", null, null)];
        }

        try
        {
            using var beforeDocument = System.Text.Json.JsonDocument.Parse(before);
            using var afterDocument = System.Text.Json.JsonDocument.Parse(after);
            var beforeValues = FlattenJson(beforeDocument.RootElement);
            var afterValues = FlattenJson(afterDocument.RootElement);
            var changes = new List<GitCommitChange>();

            foreach (var key in beforeValues.Keys.Union(afterValues.Keys).Order())
            {
                beforeValues.TryGetValue(key, out var beforeValue);
                afterValues.TryGetValue(key, out var afterValue);
                if (string.Equals(beforeValue, afterValue, StringComparison.Ordinal))
                {
                    continue;
                }

                var description = beforeValue is null
                    ? $"{FormatJsonPath(key)} が追加されました（{FormatValue(afterValue)}）"
                    : afterValue is null
                        ? $"{FormatJsonPath(key)} が削除されました"
                        : $"{FormatJsonPath(key)}：{FormatValue(beforeValue)} → {FormatValue(afterValue)}";
                changes.Add(new GitCommitChange(file.Path, description, beforeValue, afterValue));
                if (changes.Count >= 100)
                {
                    changes.Add(new GitCommitChange(file.Path, "変更が多いため、最初の100件だけ表示しています", null, null));
                    break;
                }
            }

            return changes.Count == 0
                ? [new GitCommitChange(file.Path, "ファイルが更新されました（表示できる項目の変更はありません）", null, null)]
                : changes;
        }
        catch (System.Text.Json.JsonException)
        {
            return [new GitCommitChange(file.Path, "YMMプロジェクトが更新されました", null, null)];
        }
    }

    private static Dictionary<string, string> FlattenJson(System.Text.Json.JsonElement element)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        FlattenJson(element, "$", result);
        return result;
    }

    private static void FlattenJson(
        System.Text.Json.JsonElement element,
        string path,
        Dictionary<string, string> result)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name is "ID" or "SavedState" or "TypeName")
                    {
                        continue;
                    }

                    FlattenJson(property.Value, $"{path}.{property.Name}", result);
                }
                break;
            case System.Text.Json.JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    FlattenJson(item, $"{path}[{index}]", result);
                    index++;
                }
                break;
            default:
                result[path] = element.ToString();
                break;
        }
    }

    private static string FormatJsonPath(string path)
    {
        var formatted = path.TrimStart('$', '.');
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Timelines"] = "タイムライン",
            ["Items"] = "アイテム",
            ["Name"] = "名前",
            ["Text"] = "テキスト",
            ["Volume"] = "音量",
            ["FilePath"] = "素材",
            ["Length"] = "長さ",
            ["Opacity"] = "不透明度",
            ["Frame"] = "フレーム",
        };

        foreach (var replacement in replacements)
        {
            formatted = System.Text.RegularExpressions.Regex.Replace(
                formatted,
                $"(?<![A-Za-z]){System.Text.RegularExpressions.Regex.Escape(replacement.Key)}(?![A-Za-z])",
                replacement.Value,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return formatted;
    }

    private static string FormatValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "空";
        }

        var normalized = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return normalized.Length > 80 ? $"{normalized[..77]}..." : $"「{normalized}」";
    }

    private static async Task<string?> TryGetFileContentAsync(
        string root,
        string revisionAndPath,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(root, ["show", revisionAndPath], cancellationToken, allowFailure: true);
        return result.ExitCode == 0 ? result.StandardOutput : null;
    }

    public async Task InitializeRepositoryAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(workingDirectory))
        {
            throw new DirectoryNotFoundException($"フォルダーが見つかりません: {workingDirectory}");
        }

        await RunAsync(workingDirectory, ["init"], cancellationToken);
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
