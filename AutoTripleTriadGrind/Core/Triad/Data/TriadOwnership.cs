using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace AutoTripleTriadGrind.Core.Triad.Data;

internal static unsafe class TriadOwnership
{
    private const int RefreshIntervalMs = 1_000;
    // Rows below this are placeholder NPCs the beaten bitfield does not cover.
    private const uint FirstTrackedNpcRow = 0x230002;

    private static bool[] owned = [];
    private static long nextRefreshAtMs;

    public static int OwnedCount { get; private set; }

    public static bool IsOwned(int cardId) => cardId > 0 && cardId < owned.Length && owned[cardId];

    public static void Refresh(bool force = false)
    {
        var now = Environment.TickCount64;
        if (!force && now < nextRefreshAtMs)
        {
            return;
        }

        nextRefreshAtMs = now + RefreshIntervalMs;
        var set = TriadData.Set;
        if (owned.Length != set.Cards.Length)
        {
            owned = new bool[set.Cards.Length];
        }

        var uiState = UIState.Instance();
        var count = 0;
        var cardIds = set.CardIdsInOrder;
        for (var index = 0; index < cardIds.Length; index++)
        {
            var cardId = cardIds[index];
            var isOwned = uiState != null && uiState->IsTripleTriadCardUnlocked(cardId);
            owned[cardId] = isOwned;
            if (isOwned)
            {
                count++;
            }
        }

        OwnedCount = count;
    }

    public static bool IsNpcBeaten(uint triadRowId)
    {
        if (triadRowId < FirstTrackedNpcRow)
        {
            return false;
        }

        var uiState = UIState.Instance();
        return uiState != null && uiState->IsTripleTriadNpcBeaten(triadRowId);
    }

    public static int MissingRewardCount(int npcIndex)
    {
        var rewards = TriadData.Set.RewardsOf(npcIndex);
        var missing = 0;
        for (var index = 0; index < rewards.Length; index++)
        {
            if (!IsOwned(rewards[index]))
            {
                missing++;
            }
        }

        return missing;
    }
}
