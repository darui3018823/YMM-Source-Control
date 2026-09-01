namespace YmmSourceControl.Core.Git;

public sealed class GitPushRejectedException(string message, int exitCode, string standardError)
    : GitCommandException(message, exitCode, standardError);
