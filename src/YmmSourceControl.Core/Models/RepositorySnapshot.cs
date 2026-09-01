namespace YmmSourceControl.Core.Models;

public sealed record RepositorySnapshot(
    string RootPath,
    string Branch,
    IReadOnlyList<GitFileStatus> Files)
{
    public IReadOnlyList<GitFileStatus> Changes => Files.Where(x => !x.IsStaged || x.IsUntracked).ToArray();

    public IReadOnlyList<GitFileStatus> Staged => Files.Where(x => x.IsStaged).ToArray();
}
