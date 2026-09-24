namespace AutoTripleTriadGrind.Core.Triad.Data;

internal static class TriadData
{
    public const ushort NoNpc = ushort.MaxValue;

    private static TriadDataSet? current;

    public static bool Loaded => current is not null;

    public static string FailureReason { get; private set; } = string.Empty;

    public static TriadDataSet Set => current ?? TriadDataSet.Empty;

    public static void EnsureLoaded()
    {
        if (current is not null || FailureReason.Length > 0)
        {
            return;
        }

        if (TriadDataLoader.TryLoad(out var loaded, out var failure))
        {
            current = loaded;
            return;
        }

        FailureReason = failure;
    }
}

internal sealed class TriadDataSet
{
    public static readonly TriadDataSet Empty = new();

    public TriadCard[] Cards { get; init; } = [];
    public string[] CardNames { get; init; } = [];
    public uint[] CardItemIds { get; init; } = [];
    public ushort[] CardIdsInOrder { get; init; } = [];

    public TriadNpc[] Npcs { get; init; } = [];
    public string[] NpcNames { get; init; } = [];
    public string[] NpcUnlockQuestNames { get; init; } = [];
    public ushort[] NpcDeckCards { get; init; } = [];
    public ushort[] NpcRewardCards { get; init; } = [];

    public int[] CardDropStart { get; init; } = [0];
    public ushort[] CardDropNpcs { get; init; } = [];

    public string[] RuleNames { get; init; } = new string[TriadRuleIds.Count];

    public Dictionary<uint, ushort> NpcIndexByTriadRowId { get; init; } = [];
    public Dictionary<uint, ushort> CardIdByItemId { get; init; } = [];

    public int CardCount => CardIdsInOrder.Length;

    public int NpcCount => Npcs.Length;

    public bool IsCard(int cardId) => cardId > 0 && cardId < Cards.Length && Cards[cardId].IsValid;

    public ReadOnlySpan<ushort> RewardsOf(int npcIndex)
    {
        var npc = Npcs[npcIndex];
        return NpcRewardCards.AsSpan(npc.RewardStart, npc.RewardCount);
    }

    public ReadOnlySpan<ushort> FixedCardsOf(int npcIndex)
    {
        var npc = Npcs[npcIndex];
        return NpcDeckCards.AsSpan(npc.CardStart, npc.FixedCardCount);
    }

    public ReadOnlySpan<ushort> VariableCardsOf(int npcIndex)
    {
        var npc = Npcs[npcIndex];
        return NpcDeckCards.AsSpan(npc.CardStart + TriadNpc.DeckSlots, npc.VariableCardCount);
    }

    public ReadOnlySpan<ushort> NpcsDropping(int cardId)
    {
        if (cardId <= 0 || cardId + 1 >= CardDropStart.Length)
        {
            return [];
        }

        var start = CardDropStart[cardId];
        return CardDropNpcs.AsSpan(start, CardDropStart[cardId + 1] - start);
    }

    public bool IsNpcReward(int cardId) => NpcsDropping(cardId).Length > 0;

    public string CardName(int cardId) => IsCard(cardId) ? CardNames[cardId] : string.Empty;
}
