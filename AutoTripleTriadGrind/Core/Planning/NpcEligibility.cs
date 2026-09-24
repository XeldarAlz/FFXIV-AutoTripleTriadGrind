using AutoTripleTriadGrind.Core.Triad.Data;

namespace AutoTripleTriadGrind.Core.Planning;

internal static class NpcEligibility
{
    public static SkipReason Check(int npcIndex, Configuration configuration)
    {
        var npc = TriadData.Set.Npcs[npcIndex];
        if (npc.TerritoryId == TriadDataLoader.BattleHallTerritoryId)
        {
            return SkipReason.BattleHall;
        }

        if (configuration.MaxMatchFee > 0 && npc.Fee > configuration.MaxMatchFee)
        {
            return SkipReason.FeeTooHigh;
        }

        return TriadUnlock.IsUnlocked(npcIndex) ? SkipReason.None : SkipReason.Locked;
    }
}
