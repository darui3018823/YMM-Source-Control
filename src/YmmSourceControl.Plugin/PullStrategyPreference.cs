using YmmSourceControl.Core.Models;

namespace YmmSourceControl.Plugin;

public enum PullStrategyPreference
{
    Ask,
    Merge,
    Rebase,
}

public static class PullStrategyPreferenceExtensions
{
    public static PullStrategy? ToStrategy(this PullStrategyPreference preference) => preference switch
    {
        PullStrategyPreference.Merge => PullStrategy.Merge,
        PullStrategyPreference.Rebase => PullStrategy.Rebase,
        _ => null,
    };

    public static PullStrategyPreference ToPreference(this PullStrategy strategy) => strategy switch
    {
        PullStrategy.Rebase => PullStrategyPreference.Rebase,
        _ => PullStrategyPreference.Merge,
    };
}
