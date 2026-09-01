namespace YmmSourceControl.Core.Models;

public sealed record GitCommitChange(
    string FilePath,
    string Description,
    string? Before,
    string? After)
{
    public string FileName => System.IO.Path.GetFileName(FilePath);
}
