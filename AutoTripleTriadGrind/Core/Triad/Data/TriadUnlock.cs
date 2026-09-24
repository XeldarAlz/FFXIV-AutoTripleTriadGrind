using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace AutoTripleTriadGrind.Core.Triad.Data;

internal static unsafe class TriadUnlock
{
    // Quest sheet row ids carry this offset over the ids the quest manager keys completion by.
    private const uint QuestRowOffset = 65536;

    public static bool IsUnlocked(int npcIndex)
    {
        var npc = TriadData.Set.Npcs[npcIndex];
        return IsQuestDone(npc.UnlockQuestId) || TriadOwnership.IsNpcBeaten(npc.TriadRowId);
    }

    public static bool IsQuestDone(uint questRowId)
    {
        if (questRowId == 0)
        {
            return true;
        }

        var shortId = questRowId & 0xFFFF;
        var shifted = questRowId > QuestRowOffset ? questRowId - QuestRowOffset : questRowId + QuestRowOffset;
        if (QuestManager.IsQuestComplete(questRowId) || QuestManager.IsQuestComplete(shortId) || QuestManager.IsQuestComplete(shifted))
        {
            return true;
        }

        var uiState = UIState.Instance();
        return uiState != null
            && (uiState->IsUnlockLinkUnlockedOrQuestCompleted(questRowId)
                || uiState->IsUnlockLinkUnlockedOrQuestCompleted(shortId)
                || uiState->IsUnlockLinkUnlockedOrQuestCompleted(shifted));
    }
}
