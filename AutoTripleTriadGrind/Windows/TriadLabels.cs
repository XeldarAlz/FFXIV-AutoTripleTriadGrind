using AutoTripleTriadGrind.Core.Localization;
using AutoTripleTriadGrind.Core.Planning;
using AutoTripleTriadGrind.Core.Travel;
using AutoTripleTriadGrind.Core.Triad.Data;

namespace AutoTripleTriadGrind.Windows;

// Per-NPC and per-card strings built once per data load and language, so the library does not format them every frame.
internal static class TriadLabels
{
    private static TriadDataSet? builtFor;
    private static LanguageInfo? builtLanguage;
    private static string[] npcLocations = [];
    private static string[] npcSearchKeys = [];
    private static string[] cardSearchKeys = [];
    private static string[] cardStats = [];

    public static string NpcLocation(int npcIndex)
    {
        EnsureBuilt();
        return npcLocations[npcIndex];
    }

    public static string NpcSearchKey(int npcIndex)
    {
        EnsureBuilt();
        return npcSearchKeys[npcIndex];
    }

    public static string CardSearchKey(int cardId)
    {
        EnsureBuilt();
        return cardSearchKeys[cardId];
    }

    public static string CardStats(int cardId)
    {
        EnsureBuilt();
        return cardStats[cardId];
    }

    public static string Expansion(byte expansion) => expansion switch
    {
        0 => Loc.T(L.Triad.ExpansionArr),
        1 => Loc.T(L.Triad.ExpansionHw),
        2 => Loc.T(L.Triad.ExpansionSb),
        3 => Loc.T(L.Triad.ExpansionShb),
        4 => Loc.T(L.Triad.ExpansionEw),
        _ => Loc.T(L.Triad.ExpansionDt),
    };

    public static string Skip(SkipReason reason) => reason switch
    {
        SkipReason.Locked         => Loc.T(L.Triad.SkipLocked),
        SkipReason.BattleHall     => Loc.T(L.Triad.SkipBattleHall),
        SkipReason.FeeTooHigh     => Loc.T(L.Triad.SkipFee),
        SkipReason.NotEnoughMgp   => Loc.T(L.Triad.SkipMgp),
        SkipReason.InteractFailed => Loc.T(L.Triad.SkipInteract),
        SkipReason.LossStreak     => Loc.T(L.Triad.SkipLossStreak),
        SkipReason.MatchLimit     => Loc.T(L.Triad.SkipMatchLimit),
        SkipReason.InventoryFull  => Loc.T(L.Triad.SkipInventory),
        _                         => Loc.T(L.Triad.SkipUnreachable),
    };

    private static void EnsureBuilt()
    {
        var set = TriadData.Set;
        if (ReferenceEquals(builtFor, set) && ReferenceEquals(builtLanguage, Loc.Current))
        {
            return;
        }

        npcLocations = new string[set.NpcCount];
        npcSearchKeys = new string[set.NpcCount];
        for (var npcIndex = 0; npcIndex < set.NpcCount; npcIndex++)
        {
            var npc = set.Npcs[npcIndex];
            var zone = TerritoryNames.Of(npc.TerritoryId);
            npcLocations[npcIndex] = Loc.T(L.Triad.NpcLocation, zone, npc.MapCoordinates.X.ToString("0.0", Loc.Culture), npc.MapCoordinates.Y.ToString("0.0", Loc.Culture));
            npcSearchKeys[npcIndex] = string.Concat(set.NpcNames[npcIndex], " ", zone);
        }

        cardSearchKeys = new string[set.Cards.Length];
        cardStats = new string[set.Cards.Length];
        for (var cardId = 0; cardId < set.Cards.Length; cardId++)
        {
            cardSearchKeys[cardId] = set.CardNames[cardId] ?? string.Empty;
            if (!set.IsCard(cardId))
            {
                cardStats[cardId] = string.Empty;
                continue;
            }

            var card = set.Cards[cardId];
            cardStats[cardId] = Loc.T(L.Triad.CardStats, card.Stars, Side(card.Up), Side(card.Right), Side(card.Down), Side(card.Left));
        }

        builtFor = set;
        builtLanguage = Loc.Current;
    }

    // The game prints a 10 as an A on the card face.
    private static string Side(byte value) => value >= 10 ? "A" : value.ToString(Loc.Culture);
}
