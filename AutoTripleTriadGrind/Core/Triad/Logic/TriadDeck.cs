using AutoTripleTriadGrind.Core.Triad.Data;
using System.Text;

namespace AutoTripleTriadGrind.Core.Triad.Logic;

public enum TriadDeckState : byte
{
    Valid,
    MissingCards,
    HasDuplicates,
    TooMany4Star,
    TooMany5Star,
}

// Known cards are always in the hand; the unknown pool is the NPC's variable cards, of which the NPC draws the rest.
public sealed class TriadDeck
{
    public const int HandSize = 5;

    public readonly List<ushort> KnownCards;
    public readonly List<ushort> UnknownPool;

    public TriadDeck()
    {
        KnownCards = new List<ushort>(HandSize);
        UnknownPool = [];
    }

    public TriadDeck(IReadOnlyList<ushort> knownCards, IReadOnlyList<ushort>? unknownPool = null)
    {
        KnownCards = new List<ushort>(knownCards.Count);
        AddPlayable(KnownCards, knownCards);
        UnknownPool = new List<ushort>(unknownPool?.Count ?? 0);
        if (unknownPool is not null)
        {
            AddPlayable(UnknownPool, unknownPool);
        }
    }

    public TriadDeck(ReadOnlySpan<ushort> knownCards, ReadOnlySpan<ushort> unknownPool)
    {
        KnownCards = new List<ushort>(knownCards.Length);
        for (var index = 0; index < knownCards.Length; index++)
        {
            if (TriadCards.IsPlayable(knownCards[index]))
            {
                KnownCards.Add(knownCards[index]);
            }
        }

        UnknownPool = new List<ushort>(unknownPool.Length);
        for (var index = 0; index < unknownPool.Length; index++)
        {
            if (TriadCards.IsPlayable(unknownPool[index]))
            {
                UnknownPool.Add(unknownPool[index]);
            }
        }
    }

    public int CardCount => KnownCards.Count + UnknownPool.Count;

    public ushort GetCard(int index)
    {
        if (index < 0)
        {
            return TriadCards.None;
        }

        if (index < KnownCards.Count)
        {
            return KnownCards[index];
        }

        return index < CardCount ? UnknownPool[index - KnownCards.Count] : TriadCards.None;
    }

    public int GetCardIndex(ushort cardId)
    {
        var index = KnownCards.IndexOf(cardId);
        if (index >= 0)
        {
            return index;
        }

        index = UnknownPool.IndexOf(cardId);
        return index >= 0 ? index + KnownCards.Count : -1;
    }

    public bool SetCard(int index, ushort cardId)
    {
        if (index < 0)
        {
            return false;
        }

        if (index < KnownCards.Count)
        {
            KnownCards[index] = cardId;
            return true;
        }

        if (index >= CardCount)
        {
            return false;
        }

        UnknownPool[index - KnownCards.Count] = cardId;
        return true;
    }

    public TriadDeckState Validate(Func<ushort, bool> isOwned)
    {
        var fourStar = 0;
        var fiveStar = 0;
        for (var deckIndex = 0; deckIndex < KnownCards.Count; deckIndex++)
        {
            var cardId = KnownCards[deckIndex];
            if (!isOwned(cardId))
            {
                return TriadDeckState.MissingCards;
            }

            for (var testIndex = 0; testIndex < KnownCards.Count; testIndex++)
            {
                if (testIndex != deckIndex && KnownCards[testIndex] == cardId)
                {
                    return TriadDeckState.HasDuplicates;
                }
            }

            var stars = TriadCards.Get(cardId).Stars;
            if (stars == 5)
            {
                fiveStar++;
            }
            else if (stars == 4)
            {
                fourStar++;
            }
        }

        if (fiveStar > 1)
        {
            return TriadDeckState.TooMany5Star;
        }

        return fourStar + fiveStar > 2 ? TriadDeckState.TooMany4Star : TriadDeckState.Valid;
    }

    public bool SameCards(TriadDeck other)
        => SameSet(KnownCards, other.KnownCards) && SameSet(UnknownPool, other.UnknownPool);

    public string Describe()
    {
        var builder = new StringBuilder(64);
        for (var index = 0; index < KnownCards.Count; index++)
        {
            builder.Append(index == 0 ? string.Empty : ",").Append(KnownCards[index]);
        }

        if (UnknownPool.Count == 0)
        {
            return builder.ToString();
        }

        builder.Append(" + [");
        for (var index = 0; index < UnknownPool.Count; index++)
        {
            builder.Append(index == 0 ? string.Empty : ",").Append(UnknownPool[index]);
        }

        return builder.Append(']').ToString();
    }

    private static void AddPlayable(List<ushort> target, IReadOnlyList<ushort> source)
    {
        for (var index = 0; index < source.Count; index++)
        {
            if (TriadCards.IsPlayable(source[index]))
            {
                target.Add(source[index]);
            }
        }
    }

    private static bool SameSet(List<ushort> left, List<ushort> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!right.Contains(left[index]))
            {
                return false;
            }
        }

        return true;
    }
}
