using AutoTripleTriadGrind.Core.Planning;
using AutoTripleTriadGrind.Core.Triad.Data;
using ECommons.DalamudServices;
using System.Text;

namespace AutoTripleTriadGrind.Core.Debug;

internal static class TriadNpcDumper
{
    public static void Dump()
    {
        TriadData.EnsureLoaded();
        if (!TriadData.Loaded)
        {
            Svc.Chat.PrintError($"{AttgConstants.LogPrefix} Triple Triad data is not loaded: {TriadData.FailureReason}");
            return;
        }

        TriadOwnership.Refresh(force: true);
        var set = TriadData.Set;
        var configuration = Plugin.Instance.Configuration;
        var builder = new StringBuilder(64 * 1024);
        for (var npcIndex = 0; npcIndex < set.NpcCount; npcIndex++)
        {
            var npc = set.Npcs[npcIndex];
            builder.Clear();
            builder.Append("NPC ").Append(npcIndex).Append(" row=").Append(npc.TriadRowId).Append(" enpc=").Append(npc.ENpcBaseId)
                .Append(" '").Append(set.NpcNames[npcIndex]).Append("' territory=").Append(npc.TerritoryId)
                .Append(" pos=").Append(npc.Position).Append(" fee=").Append(npc.Fee).Append(" rules=0x").Append(npc.RuleMask.ToString("X4"))
                .Append(" eligibility=").Append(NpcEligibility.Check(npcIndex, configuration))
                .Append(" beaten=").Append(TriadOwnership.IsNpcBeaten(npc.TriadRowId)).Append(" rewards=[");
            var rewards = set.RewardsOf(npcIndex);
            for (var rewardIndex = 0; rewardIndex < rewards.Length; rewardIndex++)
            {
                var cardId = rewards[rewardIndex];
                builder.Append(rewardIndex == 0 ? string.Empty : ", ").Append(set.CardNames[cardId]).Append(TriadOwnership.IsOwned(cardId) ? " (owned)" : string.Empty);
            }

            builder.Append(']');
            RunLog.Info(builder.ToString());
        }

        Svc.Chat.Print($"{AttgConstants.LogPrefix} Wrote {set.NpcCount} Triple Triad NPCs to the plugin log; you own {TriadOwnership.OwnedCount} of {set.CardCount} cards.");
    }
}
