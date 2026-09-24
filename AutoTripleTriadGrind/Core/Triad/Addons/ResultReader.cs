using AutoTripleTriadGrind.Core.Triad.Match;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoTripleTriadGrind.Core.Triad.Addons;

internal static unsafe class ResultReader
{
    private const int ExpandedRootChildren = 10;
    private const int FlagsNodeIndex = 9;
    // The outcome is three banners, draw, loss and win, of which one is visible.
    private const int FlagChildren = 3;

    public static MatchOutcome ReadOutcome(AtkUnitBase* result)
    {
        var root = result->RootNode;
        var outcome = FromFlags(NodeWalk.Child(root, FlagsNodeIndex, ExpandedRootChildren));
        if (outcome != MatchOutcome.Unknown)
        {
            return outcome;
        }

        var count = NodeWalk.ChildCount(root);
        for (var index = 0; index < count; index++)
        {
            outcome = FromFlags(NodeWalk.Child(root, index, count));
            if (outcome != MatchOutcome.Unknown)
            {
                return outcome;
            }
        }

        return MatchOutcome.Unknown;
    }

    public static bool IsReady(AtkUnitBase* result) => result is not null && result->IsReady;

    private static MatchOutcome FromFlags(AtkResNode* flags)
    {
        if (NodeWalk.ChildCount(flags) != FlagChildren)
        {
            return MatchOutcome.Unknown;
        }

        if (NodeWalk.IsVisible(NodeWalk.Child(flags, 2, FlagChildren)))
        {
            return MatchOutcome.Won;
        }

        if (NodeWalk.IsVisible(NodeWalk.Child(flags, 1, FlagChildren)))
        {
            return MatchOutcome.Lost;
        }

        return NodeWalk.IsVisible(NodeWalk.Child(flags, 0, FlagChildren)) ? MatchOutcome.Drawn : MatchOutcome.Unknown;
    }
}
