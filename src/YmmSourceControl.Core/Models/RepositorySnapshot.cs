namespace YmmSourceControl.Core.Models;

public sealed record RepositorySnapshot(
    string RootPath,
    string Branch,
    IReadOnlyList<GitFileStatus> Files,
    IReadOnlyList<string> Remotes,
    string? Upstream,
    int Ahead,
    int Behind,
    GitOperationState OperationState)
{
    public IReadOnlyList<GitFileStatus> Changes => Files
        .Where(x => x.Kind != GitFileKind.Conflicted && (!x.IsStaged || x.IsWorktreeChanged))
        .ToArray();

    public IReadOnlyList<GitFileStatus> Staged => Files
        .Where(x => x.Kind != GitFileKind.Conflicted && x.IsStaged)
        .ToArray();

    public IReadOnlyList<GitFileStatus> Conflicts => Files
        .Where(x => x.Kind == GitFileKind.Conflicted)
        .ToArray();

    public bool HasRemote => Remotes.Count > 0;
}
