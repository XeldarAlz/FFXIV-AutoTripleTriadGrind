using AutoTripleTriadGrind.Core.Triad.Data;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoTripleTriadGrind.Core.Triad.Addons;

// The challenge window lists the NPC's own rules and, below them, the rules of the area it plays in.
internal static unsafe class RequestReader
{
    private const int MinRootChildren = 9;
    private const int RegionalRulesIndex = 7;
    private const int RuleEntryChildren = 3;
    private const int RuleTextIndex = 2;
    private const int RegionalSlots = 2;

    public static bool TryReadRegionalRules(AtkUnitBase* request, Span<TriadRuleId> regional)
    {
        regional.Clear();
        var root = request->RootNode;
        var count = NodeWalk.ChildCount(root);
        if (count < MinRootChildren)
        {
            return false;
        }

        var group = NodeWalk.Child(root, RegionalRulesIndex, count);
        var entries = NodeWalk.ChildCount(group);
        if (entries == 0)
        {
            return false;
        }

        var found = 0;
        for (var entry = 0; entry < Math.Min(entries, RegionalSlots); entry++)
        {
            var text = NodeWalk.Text(NodeWalk.ComponentChild(NodeWalk.Child(group, entry, entries), RuleTextIndex, RuleEntryChildren));
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var rule = TriadRuleParser.Find(text.Trim());
            if (rule != TriadRuleId.None)
            {
                regional[found++] = rule;
            }
        }

        return true;
    }
}
