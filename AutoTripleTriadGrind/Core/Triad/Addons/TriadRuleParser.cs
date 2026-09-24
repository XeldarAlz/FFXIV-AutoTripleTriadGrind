using AutoTripleTriadGrind.Core.Triad.Data;
using AutoTripleTriadGrind.Core.Triad.Logic.Rules;

namespace AutoTripleTriadGrind.Core.Triad.Addons;

// The windows print rule names from the same sheet the plugin loads, in the client language, so a name matches
// exactly. Roulette shows the rule it rolled in parentheses once the match has started.
internal static class TriadRuleParser
{
    public static TriadRule? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        var direct = Find(trimmed);
        if (direct != TriadRuleId.None)
        {
            return TriadRules.Create(direct);
        }

        var open = trimmed.IndexOf('(');
        var close = trimmed.LastIndexOf(')');
        if (open <= 0 || close <= open)
        {
            return null;
        }

        var outer = Find(trimmed[..open].Trim());
        var inner = Find(trimmed[(open + 1)..close].Trim());
        if (outer != TriadRuleId.Roulette && inner == TriadRuleId.None)
        {
            return null;
        }

        var roulette = new TriadRuleRoulette();
        if (inner is not (TriadRuleId.None or TriadRuleId.Roulette))
        {
            roulette.Resolve(TriadRules.Create(inner));
        }

        return roulette;
    }

    public static TriadRuleId Find(string name)
    {
        var names = TriadData.Set.RuleNames;
        for (var ruleIndex = 1; ruleIndex < TriadRuleIds.Count; ruleIndex++)
        {
            if (names[ruleIndex] is { Length: > 0 } ruleName && ruleName.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return (TriadRuleId)ruleIndex;
            }
        }

        return TriadRuleId.None;
    }
}
