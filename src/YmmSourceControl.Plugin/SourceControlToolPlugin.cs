using YukkuriMovieMaker.Plugin;

namespace YmmSourceControl.Plugin;

public sealed class SourceControlToolPlugin : IToolPlugin
{
    public string Name => "Source Control";

    public Type ViewModelType => typeof(SourceControlToolViewModel);

    public Type ViewType => typeof(SourceControlToolView);

    public bool AllowMultipleInstances => false;
}

