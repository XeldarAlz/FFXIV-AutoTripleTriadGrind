using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System.Numerics;
using TriadRuleSheet = Lumina.Excel.Sheets.TripleTriadRule;
using TriadSheet = Lumina.Excel.Sheets.TripleTriad;

namespace AutoTripleTriadGrind.Core.Triad.Data;

internal static class TriadDataLoader
{
    private const byte LevelTypeNpc = 8;
    private const int CardTypeRows = 5;
    private const int CardRarityRows = 6;
    // ItemUICategory 86 is "Triple Triad Card".
    private const uint TriadCardItemCategory = 86;
    // The Battle Hall is a Duty Finder instance, so its NPCs cannot be walked to.
    public const uint BattleHallTerritoryId = 579;

    private static readonly string[] casingExclusions = ["the", "goe", "van", "des", "sas", "yae", "tol", "der", "rem"];

    public static bool TryLoad(out TriadDataSet loaded, out string failure)
    {
        loaded = TriadDataSet.Empty;
        try
        {
            return TryBuild(out loaded, out failure);
        }
        catch (Exception exception)
        {
            failure = $"Reading the Triple Triad game data failed: {exception.Message}";
            RunLog.Error(exception, failure);
            return false;
        }
    }

    private static bool TryBuild(out TriadDataSet loaded, out string failure)
    {
        loaded = TriadDataSet.Empty;
        var ruleSheet = Svc.Data.GetExcelSheet<TriadRuleSheet>();
        if (ruleSheet.Count != TriadRuleIds.Count)
        {
            failure = $"The game has {ruleSheet.Count} Triple Triad rules where {TriadRuleIds.Count} are known; the plugin needs an update.";
            return false;
        }

        if (Svc.Data.GetExcelSheet<TripleTriadCardType>().Count != CardTypeRows || Svc.Data.GetExcelSheet<TripleTriadCardRarity>().Count != CardRarityRows)
        {
            failure = "The game's card type or rarity tables changed shape; the plugin needs an update.";
            return false;
        }

        var ruleNames = new string[TriadRuleIds.Count];
        for (var ruleIndex = 0; ruleIndex < TriadRuleIds.Count; ruleIndex++)
        {
            ruleNames[ruleIndex] = ruleSheet.GetRowOrDefault((uint)ruleIndex)?.Name.ExtractText() ?? ((TriadRuleId)ruleIndex).ToString();
        }

        if (!TryReadCards(out var cards, out var cardNames, out failure))
        {
            return false;
        }

        var cardItemIds = new uint[cards.Length];
        var cardIdByItemId = ReadCardItems(cards, cardItemIds);
        var npcs = new List<TriadNpc>();
        var npcNames = new List<string>();
        var questNames = new List<string>();
        var deckCards = new List<ushort>();
        var rewardCards = new List<ushort>();
        ReadNpcs(cards, cardIdByItemId, npcs, npcNames, questNames, deckCards, rewardCards);

        var npcArray = npcs.ToArray();
        var npcIndexByRow = new Dictionary<uint, ushort>(npcArray.Length);
        for (var npcIndex = 0; npcIndex < npcArray.Length; npcIndex++)
        {
            npcIndexByRow[npcArray[npcIndex].TriadRowId] = (ushort)npcIndex;
        }

        var rewardArray = rewardCards.ToArray();
        BuildDropIndex(cards.Length, npcArray, rewardArray, out var dropStart, out var dropNpcs);

        loaded = new TriadDataSet
        {
            Cards = cards,
            CardNames = cardNames,
            CardItemIds = cardItemIds,
            CardIdsInOrder = SortedCardIds(cards),
            Npcs = npcArray,
            NpcNames = npcNames.ToArray(),
            NpcUnlockQuestNames = questNames.ToArray(),
            NpcDeckCards = deckCards.ToArray(),
            NpcRewardCards = rewardArray,
            CardDropStart = dropStart,
            CardDropNpcs = dropNpcs,
            RuleNames = ruleNames,
            NpcIndexByTriadRowId = npcIndexByRow,
            CardIdByItemId = cardIdByItemId,
        };
        RunLog.Info($"Loaded Triple Triad data: {loaded.CardCount} cards, {npcArray.Length} NPCs, {rewardArray.Length} NPC rewards.");
        failure = string.Empty;
        return true;
    }

    private static bool TryReadCards(out TriadCard[] cards, out string[] names, out string failure)
    {
        var residentSheet = Svc.Data.GetExcelSheet<TripleTriadCardResident>();
        var nameSheet = Svc.Data.GetExcelSheet<TripleTriadCard>();
        cards = [];
        names = [];
        if (residentSheet.Count != nameSheet.Count)
        {
            failure = $"The card tables disagree ({residentSheet.Count} stat rows, {nameSheet.Count} name rows); the plugin needs an update.";
            return false;
        }

        uint highestRowId = 0;
        for (var rowIndex = 0; rowIndex < residentSheet.Count; rowIndex++)
        {
            highestRowId = Math.Max(highestRowId, residentSheet.GetRowAt(rowIndex).RowId);
        }

        cards = new TriadCard[highestRowId + 1];
        names = new string[highestRowId + 1];
        for (var rowIndex = 0; rowIndex < residentSheet.Count; rowIndex++)
        {
            var resident = residentSheet.GetRowAt(rowIndex);
            var rowId = resident.RowId;
            names[rowId] = string.Empty;
            if (rowId == 0 || resident.Top == 0 || rowId > ushort.MaxValue)
            {
                continue;
            }

            var rarityRow = resident.TripleTriadCardRarity.RowId;
            var stars = (byte)Math.Clamp(rarityRow, 1, 5);
            var type = resident.TripleTriadCardType.RowId < CardTypeRows ? (TriadCardType)resident.TripleTriadCardType.RowId : TriadCardType.None;
            cards[rowId] = new TriadCard((ushort)rowId, resident.Top, resident.Right, resident.Bottom, resident.Left, type, stars, resident.Order, resident.UIPriority);
            names[rowId] = FixNameCasing(nameSheet.GetRowOrDefault(rowId)?.Name.ExtractText() ?? string.Empty);
        }

        failure = string.Empty;
        return true;
    }

    private static Dictionary<uint, ushort> ReadCardItems(TriadCard[] cards, uint[] cardItemIds)
    {
        var itemSheet = Svc.Data.GetExcelSheet<Item>();
        var byItem = new Dictionary<uint, ushort>();
        for (var rowIndex = 0; rowIndex < itemSheet.Count; rowIndex++)
        {
            var item = itemSheet.GetRowAt(rowIndex);
            if (item.ItemUICategory.RowId != TriadCardItemCategory)
            {
                continue;
            }

            var cardId = item.AdditionalData.RowId;
            if (cardId == 0 || cardId >= cards.Length || !cards[cardId].IsValid)
            {
                continue;
            }

            byItem[item.RowId] = (ushort)cardId;
            if (cardItemIds[cardId] == 0)
            {
                cardItemIds[cardId] = item.RowId;
            }
        }

        return byItem;
    }

    private static void ReadNpcs(TriadCard[] cards, Dictionary<uint, ushort> cardIdByItemId, List<TriadNpc> npcs, List<string> names,
        List<string> questNames, List<ushort> deckCards, List<ushort> rewardCards)
    {
        var triadSheet = Svc.Data.GetExcelSheet<TriadSheet>();
        var eNpcByTriadRow = MapTriadRowsToENpcs(triadSheet);
        var levels = ReadNpcLevels(eNpcByTriadRow);
        var residentSheet = Svc.Data.GetExcelSheet<ENpcResident>();
        var mapSheet = Svc.Data.GetExcelSheet<Map>();
        var territorySheet = Svc.Data.GetExcelSheet<TerritoryType>();
        var questSheet = Svc.Data.GetExcelSheet<Quest>();

        for (var rowIndex = 0; rowIndex < triadSheet.Count; rowIndex++)
        {
            var row = triadSheet.GetRowAt(rowIndex);
            if (row.RowId == 0 || !eNpcByTriadRow.TryGetValue(row.RowId, out var eNpcId))
            {
                continue;
            }

            if (!levels.TryGetValue(eNpcId, out var level))
            {
                RunLog.Debug($"Triad NPC row {row.RowId} (ENpc {eNpcId}) has no map position; left out.");
                continue;
            }

            var cardStart = deckCards.Count;
            var fixedCount = AppendDeck(row.TripleTriadCardFixed, cards, deckCards);
            var variableCount = AppendDeck(row.TripleTriadCardVariable, cards, deckCards);
            if (fixedCount == 0 && variableCount == 0)
            {
                deckCards.RemoveRange(cardStart, deckCards.Count - cardStart);
                continue;
            }

            var rewardStart = rewardCards.Count;
            for (var rewardIndex = 0; rewardIndex < row.ItemPossibleReward.Count; rewardIndex++)
            {
                var itemId = row.ItemPossibleReward[rewardIndex].RowId;
                if (itemId != 0 && cardIdByItemId.TryGetValue(itemId, out var cardId) && !ContainsCard(rewardCards, rewardStart, cardId))
                {
                    rewardCards.Add(cardId);
                }
            }

            ushort ruleMask = 0;
            for (var ruleIndex = 0; ruleIndex < row.TripleTriadRule.Count; ruleIndex++)
            {
                var ruleId = row.TripleTriadRule[ruleIndex].RowId;
                if (ruleId is > 0 and < TriadRuleIds.Count)
                {
                    ruleMask = TriadRuleIds.With(ruleMask, (TriadRuleId)ruleId);
                }
            }

            uint unlockQuestId = 0;
            for (var questIndex = 0; questIndex < row.PreviousQuest.Count; questIndex++)
            {
                if (row.PreviousQuest[questIndex].RowId == 0)
                {
                    continue;
                }

                unlockQuestId = row.PreviousQuest[questIndex].RowId;
                break;
            }

            var map = mapSheet.GetRowOrDefault(level.MapId);
            var territoryId = map is { TerritoryType.RowId: not 0 } ? map.Value.TerritoryType.RowId : level.TerritoryId;
            var mapCoordinates = map is null
                ? Vector2.Zero
                : new Vector2(ToMapCoordinate(level.Position.X, map.Value.SizeFactor, map.Value.OffsetX), ToMapCoordinate(level.Position.Z, map.Value.SizeFactor, map.Value.OffsetY));
            var expansion = (byte)(territorySheet.GetRowOrDefault(territoryId)?.ExVersion.RowId ?? 0);

            npcs.Add(new TriadNpc(row.RowId, eNpcId, territoryId, level.MapId, level.Position, mapCoordinates, unlockQuestId, row.Fee,
                ruleMask, row.UsesRegionalRules, expansion, cardStart, (byte)fixedCount, (byte)variableCount, rewardStart, (byte)(rewardCards.Count - rewardStart)));
            names.Add(FixNameCasing(residentSheet.GetRowOrDefault(eNpcId)?.Singular.ExtractText() ?? $"NPC {eNpcId}"));
            questNames.Add(unlockQuestId == 0 ? string.Empty : questSheet.GetRowOrDefault(unlockQuestId)?.Name.ExtractText() ?? string.Empty);
        }
    }

    private static int AppendDeck(Lumina.Excel.Collection<Lumina.Excel.RowRef<TripleTriadCard>> slots, TriadCard[] cards, List<ushort> deckCards)
    {
        var count = 0;
        for (var slotIndex = 0; slotIndex < TriadNpc.DeckSlots; slotIndex++)
        {
            var cardId = slotIndex < slots.Count ? slots[slotIndex].RowId : 0;
            if (cardId != 0 && cardId < cards.Length && cards[cardId].IsValid)
            {
                deckCards.Add((ushort)cardId);
                count++;
            }
        }

        for (var padIndex = count; padIndex < TriadNpc.DeckSlots; padIndex++)
        {
            deckCards.Add(0);
        }

        return count;
    }

    private static bool ContainsCard(List<ushort> cards, int start, ushort cardId)
    {
        for (var index = start; index < cards.Count; index++)
        {
            if (cards[index] == cardId)
            {
                return true;
            }
        }

        return false;
    }

    private static Dictionary<uint, uint> MapTriadRowsToENpcs(Lumina.Excel.ExcelSheet<TriadSheet> triadSheet)
    {
        var triadRows = new HashSet<uint>(triadSheet.Count);
        for (var rowIndex = 0; rowIndex < triadSheet.Count; rowIndex++)
        {
            var rowId = triadSheet.GetRowAt(rowIndex).RowId;
            if (rowId != 0)
            {
                triadRows.Add(rowId);
            }
        }

        var eNpcSheet = Svc.Data.GetExcelSheet<ENpcBase>();
        var byTriadRow = new Dictionary<uint, uint>();
        for (var rowIndex = 0; rowIndex < eNpcSheet.Count; rowIndex++)
        {
            var eNpc = eNpcSheet.GetRowAt(rowIndex);
            for (var dataIndex = 0; dataIndex < eNpc.ENpcData.Count; dataIndex++)
            {
                var dataRow = eNpc.ENpcData[dataIndex].RowId;
                if (!triadRows.Contains(dataRow))
                {
                    continue;
                }

                byTriadRow.TryAdd(dataRow, eNpc.RowId);
                break;
            }
        }

        return byTriadRow;
    }

    private readonly record struct NpcLevel(Vector3 Position, uint MapId, uint TerritoryId);

    private static Dictionary<uint, NpcLevel> ReadNpcLevels(Dictionary<uint, uint> eNpcByTriadRow)
    {
        var wanted = new HashSet<uint>(eNpcByTriadRow.Values);
        var levelSheet = Svc.Data.GetExcelSheet<Level>();
        var levels = new Dictionary<uint, NpcLevel>(wanted.Count);
        for (var rowIndex = 0; rowIndex < levelSheet.Count; rowIndex++)
        {
            var level = levelSheet.GetRowAt(rowIndex);
            if (level.Type != LevelTypeNpc || !wanted.Contains(level.Object.RowId))
            {
                continue;
            }

            levels.TryAdd(level.Object.RowId, new NpcLevel(new Vector3(level.X, level.Y, level.Z), level.Map.RowId, level.Territory.RowId));
        }

        return levels;
    }

    private static void BuildDropIndex(int cardSlots, TriadNpc[] npcs, ushort[] rewards, out int[] dropStart, out ushort[] dropNpcs)
    {
        var counts = new int[cardSlots + 1];
        for (var npcIndex = 0; npcIndex < npcs.Length; npcIndex++)
        {
            var npc = npcs[npcIndex];
            for (var rewardIndex = 0; rewardIndex < npc.RewardCount; rewardIndex++)
            {
                counts[rewards[npc.RewardStart + rewardIndex]]++;
            }
        }

        dropStart = new int[cardSlots + 1];
        for (var cardId = 1; cardId <= cardSlots; cardId++)
        {
            dropStart[cardId] = dropStart[cardId - 1] + counts[cardId - 1];
        }

        dropNpcs = new ushort[dropStart[cardSlots]];
        var fill = new int[cardSlots];
        for (var npcIndex = 0; npcIndex < npcs.Length; npcIndex++)
        {
            var npc = npcs[npcIndex];
            for (var rewardIndex = 0; rewardIndex < npc.RewardCount; rewardIndex++)
            {
                var cardId = rewards[npc.RewardStart + rewardIndex];
                dropNpcs[dropStart[cardId] + fill[cardId]] = (ushort)npcIndex;
                fill[cardId]++;
            }
        }
    }

    private static ushort[] SortedCardIds(TriadCard[] cards)
    {
        var ids = new List<ushort>(cards.Length);
        for (var cardId = 1; cardId < cards.Length; cardId++)
        {
            if (cards[cardId].IsValid)
            {
                ids.Add((ushort)cardId);
            }
        }

        ids.Sort((left, right) =>
        {
            var byGroup = cards[left].Group.CompareTo(cards[right].Group);
            return byGroup != 0 ? byGroup : cards[left].Order.CompareTo(cards[right].Order);
        });
        return ids.ToArray();
    }

    // Inverse of the client's map coordinate: 0.02 map units per yalm, plus 2048 / SizeFactor, plus 1.
    private static float ToMapCoordinate(float world, ushort sizeFactor, short offset)
        => sizeFactor == 0 ? 0f : 0.02f * (world + offset) + 2048f / sizeFactor + 1f;

    // French and German sheets store some names in lowercase.
    private static string FixNameCasing(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var tokens = text.Split(' ');
        var changed = false;
        for (var tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
        {
            var token = tokens[tokenIndex];
            if (token.Length <= 2 || Array.IndexOf(casingExclusions, token) >= 0 || token[1] == '\'' || !char.IsLower(token, 0))
            {
                continue;
            }

            tokens[tokenIndex] = string.Concat(char.ToUpperInvariant(token[0]).ToString(), token[1..]);
            changed = true;
        }

        return changed ? string.Join(' ', tokens) : text;
    }
}
