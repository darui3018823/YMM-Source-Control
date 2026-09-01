using YmmSourceControl.Core.Models;

namespace YmmSourceControl.Core.Tests;

public sealed class GitFileStatusTests
{
    [Fact]
    public void Untracked_file_exposes_expected_status_properties()
    {
        var status = new GitFileStatus("assets\\clip.mp4", "?", "?", GitFileKind.Untracked);

        Assert.False(status.IsStaged);
        Assert.True(status.IsWorktreeChanged);
        Assert.True(status.IsUntracked);
        Assert.Equal("U", status.StatusLabel);
        Assert.Equal("clip.mp4", status.FileName);
        Assert.Equal("assets", status.DirectoryName);
    }

    [Fact]
    public void Staged_modified_file_uses_index_status()
    {
        var status = new GitFileStatus("project.ymmp", "M", " ", GitFileKind.Modified);

        Assert.True(status.IsStaged);
        Assert.False(status.IsWorktreeChanged);
        Assert.False(status.IsUntracked);
        Assert.Equal("M ", status.StatusLabel);
    }
}
