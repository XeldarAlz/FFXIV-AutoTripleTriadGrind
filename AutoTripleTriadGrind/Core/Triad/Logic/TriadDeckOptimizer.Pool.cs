using AutoTripleTriadGrind.Core.Triad.Data;
using AutoTripleTriadGrind.Core.Triad.Logic.Rules;

namespace AutoTripleTriadGrind.Core.Triad.Logic;

public sealed partial class TriadDeckOptimizer
{
    private struct CardPool
    {
        public ushort[][] PriorityLists;
        public ushort[] CommonList;
        public int[] SlotTypes;
    }

    private sealed class ScoredCard(ushort cardId, float score)
    {
        public readonly ushort CardId = cardId;
        public float Score = score;
    }

    private static readonly Comparison<ScoredCard> byScoreDescending = static (left, right) => right.Score.CompareTo(left.Score);

    // Priority lists feed the capped four and five star slots, the common list fills the rest. Reverse turns high
    // numbers into a weakness, so it drops the priority slots, and it flips Ascension and Descension for scoring.
    private bool BuildCardPool(ReadOnlySpan<ushort> ownedCards, List<TriadRule> rules)
    {
        var scoringRules = new List<TriadRule>(rules.Count);
        var hasReverse = false;
        var ascensionIndex = -1;
        var descensionIndex = -1;
        for (var index = 0; index < rules.Count; index++)
        {
            scoringRules.Add(rules[index]);
            switch (rules[index].Id)
            {
                case TriadRuleId.Reverse:
                    hasReverse = true;
                    break;
                case TriadRuleId.Ascension:
                    ascensionIndex = index;
                    break;
                case TriadRuleId.Descension:
                    descensionIndex = index;
                    break;
            }
        }

        var hasAscension = ascensionIndex >= 0;
        if (hasReverse && ascensionIndex >= 0)
        {
            hasAscension = false;
            scoringRules.RemoveAt(ascensionIndex);
            scoringRules.Add(new TriadRuleDescension());
        }
        else if (hasReverse && descensionIndex >= 0)
        {
            hasAscension = true;
            scoringRules.RemoveAt(descensionIndex);
            scoringRules.Add(new TriadRuleAscension());
        }

        var fiveStarList = new List<ScoredCard>();
        var fourStarList = new List<ScoredCard>();
        var commonList = new List<ScoredCard>();
        for (var index = 0; index < ownedCards.Length; index++)
        {
            var cardId = ownedCards[index];
            if (!TriadCards.IsPlayable(cardId))
            {
                continue;
            }

            ref readonly var card = ref TriadCards.Get(cardId);
            var scored = new ScoredCard(cardId, CardScore(card));
            for (var ruleIndex = 0; ruleIndex < scoringRules.Count; ruleIndex++)
            {
                scoringRules[ruleIndex].OnScoreCard(cardId, ref scored.Score);
            }

            if (!hasReverse)
            {
                fiveStarList.Add(scored);
                if (card.Stars <= 4)
                {
                    fourStarList.Add(scored);
                }
            }

            if (card.Stars <= CommonMaxStars)
            {
                commonList.Add(scored);
            }
        }

        pool = new CardPool { SlotTypes = new int[TriadDeck.HandSize] };
        Array.Fill(pool.SlotTypes, DeckSlotCommon);
        if (commonList.Count == 0)
        {
            return false;
        }

        // The five star slot is taken out of the four star allowance, as the game counts them together.
        var priorityCandidates = new List<ScoredCard>[] { fiveStarList, fourStarList };
        var available = new[] { FiveStarSlots, FourStarSlots - FiveStarSlots };
        var slot = deckOrderMatters ? 1 : 0;
        var usedLists = new List<List<ScoredCard>>(2);
        for (var listIndex = 0; listIndex < priorityCandidates.Length; listIndex++)
        {
            var candidates = priorityCandidates[listIndex];
            if (available[listIndex] <= 0 || candidates.Count == 0)
            {
                candidates.Clear();
                continue;
            }

            for (var count = 0; count < available[listIndex]; count++)
            {
                while (slot < pool.SlotTypes.Length - 1 && pool.SlotTypes[slot] != DeckSlotCommon)
                {
                    slot++;
                }

                pool.SlotTypes[slot] = usedLists.Count;
            }

            usedLists.Add(candidates);
        }

        if (hasAscension)
        {
            ApplyAscensionFilter(commonList, usedLists);
        }

        pool.PriorityLists = new ushort[usedLists.Count][];
        for (var listIndex = 0; listIndex < usedLists.Count; listIndex++)
        {
            pool.PriorityLists[listIndex] = TopCards(usedLists[listIndex], PriorityToBuild);
        }

        var prioritySlots = 0;
        for (var index = 0; index < pool.SlotTypes.Length; index++)
        {
            prioritySlots += pool.SlotTypes[index] >= 0 ? 1 : 0;
        }

        var commonToUse = CommonToBuild - CommonToBuild * prioritySlots * CommonPercentDroppedPerPrioritySlot / 100;
        pool.CommonList = TopCards(commonList, commonToUse);
        return true;
    }

    private static ushort[] TopCards(List<ScoredCard> cards, int limit)
    {
        cards.Sort(byScoreDescending);
        var count = Math.Min(limit, cards.Count);
        var top = new ushort[count];
        for (var index = 0; index < count; index++)
        {
            top[index] = cards[index].CardId;
        }

        return top;
    }

    // Under Ascension one card type is worth stacking: the type with the best average score across every list wins
    // and is pushed to the front of each list.
    private static void ApplyAscensionFilter(List<ScoredCard> common, List<List<ScoredCard>> priority)
    {
        const int cardTypes = 5;
        const float preferredBoost = 1000f;
        var lists = priority.Count + 1;
        var bestType = TriadCardType.None;
        var bestScore = 0f;
        for (var type = 1; type < cardTypes; type++)
        {
            var typeScore = 0f;
            for (var listIndex = 0; listIndex < lists; listIndex++)
            {
                var source = listIndex == 0 ? common : priority[listIndex - 1];
                var sum = 0f;
                var count = 0;
                for (var index = 0; index < source.Count; index++)
                {
                    if ((int)TriadCards.Get(source[index].CardId).Type != type)
                    {
                        continue;
                    }

                    sum += source[index].Score;
                    count++;
                }

                typeScore += count == 0 ? 0f : sum / count;
            }

            typeScore /= lists;
            if (bestScore <= 0f || typeScore > bestScore)
            {
                bestScore = typeScore;
                bestType = (TriadCardType)type;
            }
        }

        if (bestType == TriadCardType.None)
        {
            return;
        }

        Boost(common, bestType, preferredBoost);
        for (var listIndex = 0; listIndex < priority.Count; listIndex++)
        {
            Boost(priority[listIndex], bestType, preferredBoost);
        }
    }

    private static void Boost(List<ScoredCard> cards, TriadCardType type, float amount)
    {
        for (var index = 0; index < cards.Count; index++)
        {
            if (TriadCards.Get(cards[index].CardId).Type == type)
            {
                cards[index].Score += amount;
            }
        }
    }
}
