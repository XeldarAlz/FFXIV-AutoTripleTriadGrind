using AutoTripleTriadGrind.Core.Triad.Data;
using AutoTripleTriadGrind.Core.Triad.Logic;

namespace AutoTripleTriadGrind.Core.Triad.Addons;

// Identifies a card on the board from what the addon exposes: its four numbers, type and rarity, with the card
// texture as the tie breaker for cards that share all of those.
internal static class TriadCardMatcher
{
    private static readonly string[] texturePrefixes = ["088000/088", "087000/087"];
    private const string LegacyTexturePrefix = "082000/082";

    private static TriadDataSet? builtFor;
    private static Dictionary<long, List<ushort>> byStatsTypeStars = [];
    private static Dictionary<long, List<ushort>> byStats = [];

    // The board addon's L field holds the side the card sheet names Right, and its R field the sheet's Left.
    public static ushort Match(byte up, byte addonLeft, byte down, byte addonRight, byte type, byte rarity, string? texturePath)
    {
        EnsureBuilt();
        var right = addonLeft;
        var left = addonRight;
        var stars = (byte)Math.Clamp((int)rarity, 1, 5);
        if (byStatsTypeStars.TryGetValue(StatsKey(up, right, down, left) | ((long)type << 32) | ((long)stars << 40), out var exact))
        {
            if (exact.Count == 1)
            {
                return exact[0];
            }

            var fromTexture = FromTexture(texturePath);
            if (exact.Contains(fromTexture))
            {
                return fromTexture;
            }
        }

        if (byStats.TryGetValue(StatsKey(up, right, down, left), out var loose) && loose.Count == 1)
        {
            return loose[0];
        }

        var textured = FromTexture(texturePath);
        return TriadData.Set.IsCard(textured) ? textured : TriadCards.None;
    }

    public static ushort FromTexture(string? texturePath)
    {
        if (string.IsNullOrEmpty(texturePath) || !texturePath.EndsWith(".tex", StringComparison.OrdinalIgnoreCase))
        {
            return TriadCards.None;
        }

        var legacyIndex = texturePath.IndexOf(LegacyTexturePrefix, StringComparison.Ordinal);
        if (legacyIndex > 0 && TryReadId(texturePath, legacyIndex + LegacyTexturePrefix.Length, out var legacyId))
        {
            // Old card art sat at 082100 and 082500 onward.
            var cardId = legacyId >= 500 ? legacyId - 500 : legacyId >= 100 ? legacyId - 100 : -1;
            return cardId >= 0 ? (ushort)cardId : TriadCards.None;
        }

        for (var index = 0; index < texturePrefixes.Length; index++)
        {
            var at = texturePath.IndexOf(texturePrefixes[index], StringComparison.Ordinal);
            if (at > 0 && TryReadId(texturePath, at + texturePrefixes[index].Length, out var cardId))
            {
                return (ushort)cardId;
            }
        }

        return TriadCards.None;
    }

    private static bool TryReadId(string path, int start, out int id)
    {
        id = -1;
        return start + 3 <= path.Length && int.TryParse(path.AsSpan(start, 3), out id);
    }

    private static long StatsKey(byte up, byte right, byte down, byte left) => (long)up | ((long)right << 8) | ((long)down << 16) | ((long)left << 24);

    private static void EnsureBuilt()
    {
        var set = TriadData.Set;
        if (ReferenceEquals(builtFor, set))
        {
            return;
        }

        var exact = new Dictionary<long, List<ushort>>();
        var loose = new Dictionary<long, List<ushort>>();
        var cardIds = set.CardIdsInOrder;
        for (var index = 0; index < cardIds.Length; index++)
        {
            ref readonly var card = ref set.Cards[cardIds[index]];
            var statsKey = StatsKey(card.Up, card.Right, card.Down, card.Left);
            Add(exact, statsKey | ((long)card.Type << 32) | ((long)card.Stars << 40), card.Id);
            Add(loose, statsKey, card.Id);
        }

        byStatsTypeStars = exact;
        byStats = loose;
        builtFor = set;
    }

    private static void Add(Dictionary<long, List<ushort>> map, long key, ushort cardId)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = new List<ushort>(1);
            map[key] = list;
        }

        list.Add(cardId);
    }
}
