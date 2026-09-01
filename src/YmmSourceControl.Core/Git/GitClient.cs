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
            // git log separates formatted commits with a newline. Since each field is
            // NUL-delimited, that separator becomes part of the next commit's hash.
            var shortHash = records[index].TrimStart('\r', '\n');
            commits.Add(new GitCommit(shortHash, records[index + 1], records[index + 2], records[index + 3]));
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
        var parentCommit = await TryGetParentCommitAsync(root, commit, cancellationToken);
        var changes = new List<GitCommitChange>();

        foreach (var file in files)
        {
            if (!file.Path.EndsWith(".ymmp", StringComparison.OrdinalIgnoreCase))
            {
                changes.Add(new GitCommitChange(file.Path, DescribeFileChange(file), null, null));
                continue;
            }

            var before = parentCommit is null
                ? null
                : await TryGetFileContentAsync(root, $"{parentCommit}:{file.Path}", cancellationToken);
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
            var changes = new List<GitCommitChange>();

            CompareJson(file.Path, beforeDocument.RootElement, afterDocument.RootElement, "$", changes);
            SuppressDerivedCharacterChanges(changes);

            return changes.Count == 0
                ? [new GitCommitChange(file.Path, "ファイルが更新されました（表示できる項目の変更はありません）", null, null)]
                : changes;
        }
        catch (System.Text.Json.JsonException)
        {
            return [new GitCommitChange(file.Path, "YMMプロジェクトが更新されました", null, null)];
        }
    }

    private static void CompareJson(
        string filePath,
        System.Text.Json.JsonElement before,
        System.Text.Json.JsonElement after,
        string path,
        List<GitCommitChange> changes)
    {
        if (before.ValueKind != after.ValueKind)
        {
            AddValueChange(filePath, path, before, after, changes);
            return;
        }

        switch (after.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                var beforeProperties = before.EnumerateObject()
                    .Select(property => property.Name)
                    .ToHashSet(StringComparer.Ordinal);
                var afterProperties = after.EnumerateObject()
                    .Select(property => property.Name)
                    .ToHashSet(StringComparer.Ordinal);

                foreach (var propertyName in beforeProperties.Union(afterProperties).Order())
                {
                    if (ShouldIgnoreProperty(propertyName, path))
                    {
                        continue;
                    }

                    var propertyPath = $"{path}.{propertyName}";
                    var hasBefore = before.TryGetProperty(propertyName, out var beforeProperty);
                    var hasAfter = after.TryGetProperty(propertyName, out var afterProperty);
                    if (!hasBefore)
                    {
                        AddValueChange(filePath, propertyPath, null, afterProperty, changes);
                    }
                    else if (!hasAfter)
                    {
                        AddValueChange(filePath, propertyPath, beforeProperty, null, changes);
                    }
                    else
                    {
                        CompareJson(filePath, beforeProperty, afterProperty, propertyPath, changes);
                    }
                }
                break;
            case System.Text.Json.JsonValueKind.Array:
                CompareArray(filePath, path, before.EnumerateArray().ToArray(), after.EnumerateArray().ToArray(), changes);
                break;
            default:
                if (!string.Equals(before.ToString(), after.ToString(), StringComparison.Ordinal))
                {
                    AddValueChange(filePath, path, before, after, changes);
                }
                break;
        }
    }

    private static void CompareArray(
        string filePath,
        string path,
        IReadOnlyList<System.Text.Json.JsonElement> before,
        IReadOnlyList<System.Text.Json.JsonElement> after,
        List<GitCommitChange> changes)
    {
        var beforeIndex = CreateUniqueItemIndex(before);
        var afterIndex = CreateUniqueItemIndex(after);

        if (beforeIndex.Count > 0 || afterIndex.Count > 0)
        {
            var matchedBefore = new bool[before.Count];
            for (var afterIndexPosition = 0; afterIndexPosition < after.Count; afterIndexPosition++)
            {
                var key = GetArrayItemKey(after[afterIndexPosition]);
                if (key is not null && beforeIndex.TryGetValue(key, out var beforeIndexPosition))
                {
                    matchedBefore[beforeIndexPosition] = true;
                    CompareJson(filePath, before[beforeIndexPosition], after[afterIndexPosition], $"{path}[{afterIndexPosition}]", changes);
                }
                else
                {
                    AddArrayItemChange(filePath, path, after[afterIndexPosition], added: true, changes);
                }
            }

            for (var beforeIndexPosition = 0; beforeIndexPosition < before.Count; beforeIndexPosition++)
            {
                if (!matchedBefore[beforeIndexPosition])
                {
                    AddArrayItemChange(filePath, path, before[beforeIndexPosition], added: false, changes);
                }
            }

            return;
        }

        var commonCount = Math.Min(before.Count, after.Count);
        for (var index = 0; index < commonCount; index++)
        {
            CompareJson(filePath, before[index], after[index], $"{path}[{index}]", changes);
        }

        for (var index = commonCount; index < after.Count; index++)
        {
            AddArrayItemChange(filePath, path, after[index], added: true, changes);
        }

        for (var index = commonCount; index < before.Count; index++)
        {
            AddArrayItemChange(filePath, path, before[index], added: false, changes);
        }
    }

    private static Dictionary<string, int> CreateUniqueItemIndex(IReadOnlyList<System.Text.Json.JsonElement> items)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        var duplicates = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < items.Count; index++)
        {
            var key = GetArrayItemKey(items[index]);
            if (key is null)
            {
                continue;
            }

            if (!result.TryAdd(key, index))
            {
                duplicates.Add(key);
            }
        }

        foreach (var duplicate in duplicates)
        {
            result.Remove(duplicate);
        }

        return result;
    }

    private static string? GetArrayItemKey(System.Text.Json.JsonElement item)
    {
        if (item.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return null;
        }

        if (item.TryGetProperty("ID", out var id) && !string.IsNullOrWhiteSpace(id.ToString()))
        {
            return $"id:{id}";
        }

        var type = GetStringProperty(item, "$type");
        var name = GetStringProperty(item, "Name");
        if (!string.IsNullOrWhiteSpace(name))
        {
            return $"name:{name}";
        }

        var characterName = GetStringProperty(item, "CharacterName");
        var frame = GetStringProperty(item, "Frame");
        var layer = GetStringProperty(item, "Layer");
        return string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(frame) || string.IsNullOrWhiteSpace(layer)
            ? null
            : $"item:{type}|{characterName}|{frame}|{layer}";
    }

    private static void AddArrayItemChange(
        string filePath,
        string path,
        System.Text.Json.JsonElement item,
        bool added,
        List<GitCommitChange> changes)
    {
        var collectionName = FormatCollectionName(path);
        var description = DescribeArrayItem(collectionName, item, added);
        changes.Add(new GitCommitChange(filePath, description, null, null));
    }

    private static string DescribeArrayItem(string collectionName, System.Text.Json.JsonElement item, bool added)
    {
        var verb = added ? "追加しました" : "削除しました";
        if (collectionName == "アイテム" && item.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            var text = GetStringProperty(item, "Serif");
            if (!string.IsNullOrWhiteSpace(text))
            {
                return $"字幕「{FormatShortText(text)}」を{verb}";
            }
        }

        if (collectionName == "キャラクター" && item.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            var name = GetStringProperty(item, "Name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                return $"キャラクター「{FormatShortText(name)}」を{verb}";
            }
        }

        return $"{collectionName}を{verb}";
    }

    private static string FormatCollectionName(string path)
    {
        var propertyName = path.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        return propertyName switch
        {
            "Timelines" => "タイムライン",
            "Items" => "アイテム",
            "Characters" => "キャラクター",
            "LayerSettings" => "レイヤー設定",
            _ => "項目",
        };
    }

    private static void AddValueChange(
        string filePath,
        string path,
        System.Text.Json.JsonElement? before,
        System.Text.Json.JsonElement? after,
        List<GitCommitChange> changes)
    {
        if (changes.Count >= 20)
        {
            return;
        }

        var beforeValue = before?.ToString();
        var afterValue = after?.ToString();
        if (string.Equals(beforeValue, afterValue, StringComparison.Ordinal))
        {
            return;
        }

        var description = before is null
            ? $"{FormatJsonPath(path)} が追加されました（{FormatValue(afterValue)}）"
            : after is null
                ? $"{FormatJsonPath(path)} が削除されました"
                : $"{FormatJsonPath(path)}：{FormatValue(beforeValue)} → {FormatValue(afterValue)}";
        changes.Add(new GitCommitChange(filePath, description, beforeValue, afterValue));
    }

    private static bool ShouldIgnoreProperty(string propertyName, string path) => propertyName is
        "ID" or "SavedState" or "TypeName" or "$type" or
        "LayoutXml" or "ToolStates" or "VoiceCache" or "Hatsuon" or "Pronounce" or
        "LipSyncFrames" or "VoiceLength" or "CurrentFrame" or "MaxLayer"
        || propertyName == "Length" && path.Contains(".Timelines[", StringComparison.Ordinal);

    private static void SuppressDerivedCharacterChanges(List<GitCommitChange> changes)
    {
        if (!changes.Any(change => change.Description.StartsWith("字幕", StringComparison.Ordinal)))
        {
            return;
        }

        changes.RemoveAll(change => change.Description.StartsWith("キャラクター", StringComparison.Ordinal));
    }

    private static string? GetStringProperty(System.Text.Json.JsonElement item, string propertyName)
    {
        return item.TryGetProperty(propertyName, out var property)
            ? property.ValueKind == System.Text.Json.JsonValueKind.String ? property.GetString() : property.ToString()
            : null;
    }

    private static string FormatShortText(string value)
    {
        var normalized = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return normalized.Length > 50 ? $"{normalized[..47]}..." : normalized;
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

    private static async Task<string?> TryGetParentCommitAsync(
        string root,
        string commit,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            root,
            ["rev-list", "--parents", "-n", "1", commit],
            cancellationToken,
            allowFailure: true);
        if (result.ExitCode != 0)
        {
            return null;
        }

        var commits = result.StandardOutput.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return commits.Length > 1 ? commits[1] : null;
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
