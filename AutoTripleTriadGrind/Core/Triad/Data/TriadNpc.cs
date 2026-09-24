using System.Numerics;

namespace AutoTripleTriadGrind.Core.Triad.Data;

public readonly struct TriadNpc
{
    public const int DeckSlots = 5;

    public readonly uint TriadRowId;
    public readonly uint ENpcBaseId;
    public readonly uint TerritoryId;
    public readonly uint MapId;
    public readonly Vector3 Position;
    public readonly Vector2 MapCoordinates;
    public readonly uint UnlockQuestId;
    public readonly ushort Fee;
    public readonly ushort RuleMask;
    public readonly bool UsesRegionalRules;
    public readonly byte Expansion;
    public readonly int CardStart;
    public readonly byte FixedCardCount;
    public readonly byte VariableCardCount;
    public readonly int RewardStart;
    public readonly byte RewardCount;

    public TriadNpc(uint triadRowId, uint eNpcBaseId, uint territoryId, uint mapId, Vector3 position, Vector2 mapCoordinates, uint unlockQuestId,
        ushort fee, ushort ruleMask, bool usesRegionalRules, byte expansion, int cardStart, byte fixedCardCount, byte variableCardCount, int rewardStart, byte rewardCount)
    {
        TriadRowId = triadRowId;
        ENpcBaseId = eNpcBaseId;
        TerritoryId = territoryId;
        MapId = mapId;
        Position = position;
        MapCoordinates = mapCoordinates;
        UnlockQuestId = unlockQuestId;
        Fee = fee;
        RuleMask = ruleMask;
        UsesRegionalRules = usesRegionalRules;
        Expansion = expansion;
        CardStart = cardStart;
        FixedCardCount = fixedCardCount;
        VariableCardCount = variableCardCount;
        RewardStart = rewardStart;
        RewardCount = rewardCount;
    }

    public bool HasRule(TriadRuleId rule) => TriadRuleIds.Has(RuleMask, rule);
}
