namespace YmmSourceControl.Core.Models;

public sealed record GitCommitFile(
    string Status,
    string Path)
{
    public string FileName => System.IO.Path.GetFileName(Path);

    public string DirectoryName => System.IO.Path.GetDirectoryName(Path) ?? string.Empty;
}
