namespace YmmSourceControl.Core.Models;

public enum RemoteVisibility
{
    Private,
    Public,
    Internal,
}

public sealed record RemotePublishOptions(
    string RepositoryName,
    string RemoteName,
    string Description,
    RemoteVisibility Visibility,
    bool Push);
