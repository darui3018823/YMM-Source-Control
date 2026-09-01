namespace YmmSourceControl.Core.Models;

public enum GitFileKind
{
    Modified,
    Added,
    Deleted,
    Renamed,
    Untracked,
    Conflicted,
    Unknown,
}

public sealed record GitFileStatus(
    string Path,
    string IndexCode,
    string WorktreeCode,
    GitFileKind Kind)
{
    public bool IsStaged => IndexCode is not " " and not "?";

    public bool IsWorktreeChanged => WorktreeCode is not " ";

    public bool IsUntracked => IndexCode == "?" && WorktreeCode == "?";

    public string StatusLabel => IsUntracked ? "U" : $"{IndexCode}{WorktreeCode}";

    public string FileName => System.IO.Path.GetFileName(Path);

    public string DirectoryName => System.IO.Path.GetDirectoryName(Path) ?? string.Empty;
}

