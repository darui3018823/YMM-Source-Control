namespace YmmSourceControl.Core.Git;

public sealed class GitCommandException(string message, int exitCode, string standardError)
    : Exception(message)
{
    public int ExitCode { get; } = exitCode;

    public string StandardError { get; } = standardError;
}

