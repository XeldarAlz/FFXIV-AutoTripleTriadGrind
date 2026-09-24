using AutoTripleTriadGrind.Core.Triad.Data;

namespace AutoTripleTriadGrind.Core.Triad.Logic.Rules;

public sealed class TriadRuleNone() : TriadRule(TriadRuleId.None);

public sealed class TriadRuleThreeOpen() : TriadRule(TriadRuleId.ThreeOpen, specialRules: TriadSpecialRules.SelectVisible3);

public sealed class TriadRuleChaos() : TriadRule(TriadRuleId.Chaos, specialRules: TriadSpecialRules.BlueCardSelection);

public sealed class TriadRuleDraft() : TriadRule(TriadRuleId.Draft, specialRules: TriadSpecialRules.IgnoreOwnedCheck);

public sealed class TriadRuleRandom() : TriadRule(TriadRuleId.Random, specialRules: TriadSpecialRules.RandomizeBlueDeck);

public sealed class TriadRuleOrder() : TriadRule(TriadRuleId.Order, TriadRuleFeatures.FilterNext, deckOrderMatters: true)
{
    public override void OnFilterNextCards(TriadGameState state, ref int allowedCardsMask)
    {
        if (state.Status != TriadGameStatus.InProgressBlue || allowedCardsMask == 0)
        {
            return;
        }

        var first = state.DeckBlue.FirstAvailableCard();
        allowedCardsMask = first < 0 ? 0 : 1 << first;
    }
}

public sealed class TriadRuleAllOpen() : TriadRule(TriadRuleId.AllOpen, specialRules: TriadSpecialRules.SelectVisible5)
{
    // Turns the red cards the open rule revealed into known cards, and drops known cards that were not revealed once
    // the hand would hold more than five.
    public static void MakeKnown(TriadGameState state, IReadOnlyList<int> redIndices)
    {
        if (state.DeckRed is not TriadDeckInstanceManual redManual || redIndices.Count > TriadDeck.HandSize)
        {
            return;
        }

        var source = redManual.Source;
        var visible = new TriadDeck(source.KnownCards, source.UnknownPool);
        for (var index = 0; index < redIndices.Count; index++)
        {
            var cardIndex = redIndices[index];
            if (cardIndex < source.KnownCards.Count)
            {
                continue;
            }

            var cardId = source.UnknownPool[cardIndex - source.KnownCards.Count];
            visible.KnownCards.Add(cardId);
            visible.UnknownPool.Remove(cardId);
        }

        for (var index = 0; index < visible.KnownCards.Count && visible.KnownCards.Count > TriadDeck.HandSize; index++)
        {
            if (Contains(redIndices, redManual.GetCardIndex(visible.KnownCards[index])))
            {
                continue;
            }

            visible.KnownCards.RemoveAt(index);
            index--;
        }

        state.DeckRed = new TriadDeckInstanceManual(visible);
    }

    private static bool Contains(IReadOnlyList<int> values, int value)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (values[index] == value)
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class TriadRuleSwap() : TriadRule(TriadRuleId.Swap, specialRules: TriadSpecialRules.SwapCards)
{
    public static void SwapCards(TriadGameState state, ushort fromBlue, int blueSlot, ushort fromRed)
    {
        if (state.DeckBlue is not TriadDeckInstanceManual blueManual || state.DeckRed is not TriadDeckInstanceManual redManual)
        {
            return;
        }

        var blueSwapped = new TriadDeck(blueManual.Source.KnownCards, blueManual.Source.UnknownPool);
        var redSwapped = new TriadDeck(redManual.Source.KnownCards, redManual.Source.UnknownPool);
        redSwapped.KnownCards.Add(fromBlue);
        redSwapped.KnownCards.Remove(fromRed);
        redSwapped.UnknownPool.Remove(fromRed);
        blueSwapped.KnownCards[blueSlot] = fromRed;
        state.DeckBlue = new TriadDeckInstanceManual(blueSwapped);
        state.DeckRed = new TriadDeckInstanceManual(redSwapped);
    }
}

public sealed class TriadRuleSuddenDeath() : TriadRule(TriadRuleId.SuddenDeath, TriadRuleFeatures.AllPlaced)
{
    private const int MaxRestarts = 3;

    // A draw restarts the match with the cards each side owns on the board, plus the one card the side that played
    // fewer still holds; the side that moved second last time opens.
    public override void OnAllCardsPlaced(TriadGameState state)
    {
        if (state.Status != TriadGameStatus.BlueDraw || state.NumRestarts >= MaxRestarts)
        {
            return;
        }

        if (state.DeckBlue is not TriadDeckInstanceManual blueManual || state.DeckRed is not TriadDeckInstanceManual redManual)
        {
            return;
        }

        var blueCards = new List<ushort>(TriadDeck.HandSize);
        var redCards = new List<ushort>(TriadDeck.HandSize);
        var redUnknown = new List<ushort>();
        var board = state.Board;
        for (var cell = 0; cell < board.Length; cell++)
        {
            if (board[cell].Owner == TriadOwner.Blue)
            {
                blueCards.Add(board[cell].CardId);
            }
            else
            {
                redCards.Add(board[cell].CardId);
            }

            board[cell] = TriadBoardSlot.Empty;
        }

        if (blueManual.NumPlaced < redManual.NumPlaced)
        {
            AddFirstUnplaced(blueManual, blueCards);
            state.Status = TriadGameStatus.InProgressBlue;
        }
        else
        {
            AddFirstUnplaced(redManual, redCards);
            if (redCards.Count < blueCards.Count)
            {
                var source = redManual.Source;
                for (var poolIndex = 0; poolIndex < source.UnknownPool.Count; poolIndex++)
                {
                    if (!redManual.IsPlaced(poolIndex + source.KnownCards.Count))
                    {
                        redUnknown.Add(source.UnknownPool[poolIndex]);
                    }
                }
            }

            state.Status = TriadGameStatus.InProgressRed;
        }

        state.DeckBlue = new TriadDeckInstanceManual(new TriadDeck(blueCards));
        state.DeckRed = new TriadDeckInstanceManual(new TriadDeck(redCards, redUnknown));
        state.NumCardsPlaced = 0;
        state.NumRestarts++;
        state.ClearTypeModifiers();
    }

    private static void AddFirstUnplaced(TriadDeckInstanceManual deck, List<ushort> cards)
    {
        var known = deck.Source.KnownCards;
        for (var cardIndex = 0; cardIndex < known.Count; cardIndex++)
        {
            if (!deck.IsPlaced(cardIndex))
            {
                cards.Add(known[cardIndex]);
                return;
            }
        }
    }
}

public sealed class TriadRuleRoulette() : TriadRule(TriadRuleId.Roulette, specialRules: TriadSpecialRules.RandomizeRule)
{
    public TriadRule? Resolved { get; private set; }

    public override TriadRuleFeatures Features => Resolved?.Features ?? TriadRuleFeatures.None;

    public override TriadSpecialRules SpecialRules => BaseSpecialRules | (Resolved?.SpecialRules ?? TriadSpecialRules.None);

    public override bool AllowsCombo => Resolved?.AllowsCombo ?? BaseAllowsCombo;

    public override bool DeckOrderMatters => Resolved?.DeckOrderMatters ?? BaseDeckOrderMatters;

    public void Resolve(TriadRule? rule) => Resolved = rule;

    public override TriadRule Clone()
    {
        var clone = new TriadRuleRoulette();
        clone.Resolve(Resolved?.Clone());
        return clone;
    }

    public override void OnMatchInit() => Resolved = null;

    public override void OnCardPlaced(TriadGameState state, int boardPosition) => Resolved?.OnCardPlaced(state, boardPosition);

    public override void OnCheckCaptureNeighbors(TriadGameState state, int boardPosition, ReadOnlySpan<int> neighbors, List<int> captures)
        => Resolved?.OnCheckCaptureNeighbors(state, boardPosition, neighbors, captures);

    public override void OnCheckCaptureWeights(TriadGameState state, int boardPosition, int neighborPosition, bool reverseActive, ref int cardNumber, ref int neighborNumber)
        => Resolved?.OnCheckCaptureWeights(state, boardPosition, neighborPosition, reverseActive, ref cardNumber, ref neighborNumber);

    public override void OnCheckCaptureMath(TriadGameState state, int boardPosition, int neighborPosition, int cardNumber, int neighborNumber, ref bool captured)
        => Resolved?.OnCheckCaptureMath(state, boardPosition, neighborPosition, cardNumber, neighborNumber, ref captured);

    public override void OnPostCaptures(TriadGameState state, int boardPosition) => Resolved?.OnPostCaptures(state, boardPosition);

    public override void OnAllCardsPlaced(TriadGameState state) => Resolved?.OnAllCardsPlaced(state);

    public override void OnFilterNextCards(TriadGameState state, ref int allowedCardsMask) => Resolved?.OnFilterNextCards(state, ref allowedCardsMask);
}

public static class TriadRules
{
    public static TriadRule Create(TriadRuleId id) => id switch
    {
        TriadRuleId.Roulette    => new TriadRuleRoulette(),
        TriadRuleId.AllOpen     => new TriadRuleAllOpen(),
        TriadRuleId.ThreeOpen   => new TriadRuleThreeOpen(),
        TriadRuleId.Same        => new TriadRuleSame(),
        TriadRuleId.SuddenDeath => new TriadRuleSuddenDeath(),
        TriadRuleId.Plus        => new TriadRulePlus(),
        TriadRuleId.Random      => new TriadRuleRandom(),
        TriadRuleId.Order       => new TriadRuleOrder(),
        TriadRuleId.Chaos       => new TriadRuleChaos(),
        TriadRuleId.Reverse     => new TriadRuleReverse(),
        TriadRuleId.FallenAce   => new TriadRuleFallenAce(),
        TriadRuleId.Ascension   => new TriadRuleAscension(),
        TriadRuleId.Descension  => new TriadRuleDescension(),
        TriadRuleId.Swap        => new TriadRuleSwap(),
        TriadRuleId.Draft       => new TriadRuleDraft(),
        _                       => new TriadRuleNone(),
    };

    public static List<TriadRule> FromMask(ushort ruleMask)
    {
        var rules = new List<TriadRule>(4);
        for (var ruleIndex = 1; ruleIndex < TriadRuleIds.Count; ruleIndex++)
        {
            if (TriadRuleIds.Has(ruleMask, (TriadRuleId)ruleIndex))
            {
                rules.Add(Create((TriadRuleId)ruleIndex));
            }
        }

        return rules;
    }
}
