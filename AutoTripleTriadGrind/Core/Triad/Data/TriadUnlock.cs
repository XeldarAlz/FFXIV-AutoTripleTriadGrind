using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace AutoTripleTriadGrind.Core.Triad.Data;

internal static unsafe class TriadUnlock
{
    public const int NoMissingQuest = -1;

    public static bool IsUnlocked(int npcIndex)
        => MissingQuestSlot(npcIndex) == NoMissingQuest || TriadOwnership.IsNpcBeaten(TriadData.Set.Npcs[npcIndex].TriadRowId);

    public static int MissingQuestSlot(int npcIndex)
    {
        var set = TriadData.Set;
        var quests = set.UnlockQuestsOf(npcIndex);
        var needsAnyQuest = set.Npcs[npcIndex].UnlockNeedsAnyQuest;
        var missingSlot = NoMissingQuest;
        for (var slot = 0; slot < quests.Length; slot++)
        {
            if (quests[slot] == 0)
            {
                continue;
            }

            if (IsQuestDone(quests[slot]))
            {
                if (needsAnyQuest)
                {
                    return NoMissingQuest;
                }

                continue;
            }

            if (missingSlot == NoMissingQuest)
            {
                missingSlot = slot;
            }
        }

        return missingSlot;
    }

    private static bool IsQuestDone(uint questRowId)
    {
        if (QuestManager.IsQuestComplete(questRowId) || QuestManager.IsQuestComplete((ushort)(questRowId & ushort.MaxValue)))
        {
            return true;
        }

        var uiState = UIState.Instance();
        return uiState != null && uiState->IsUnlockLinkUnlockedOrQuestCompleted(questRowId);
    }
}
