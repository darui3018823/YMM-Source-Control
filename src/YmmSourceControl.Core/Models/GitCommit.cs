namespace YmmSourceControl.Core.Models;

public sealed record GitCommit(
    string ShortHash,
    string Author,
    string Date,
    string Subject)
{
    public string DisplayText => $"{ShortHash}  {Subject}";
}
