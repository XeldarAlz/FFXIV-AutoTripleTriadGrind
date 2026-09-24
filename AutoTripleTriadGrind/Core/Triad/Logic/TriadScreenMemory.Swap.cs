namespace AutoTripleTriadGrind.Core.Triad.Logic;

public sealed partial class TriadScreenMemory
{
    // Swap trades one card each way before the first move. The traded red card shows up in blue's hand; the blue card
    // it replaced is found by comparing the hand with the deck the player picked, or with the hands seen before.
    private TriadScreenUpdate DetectSwapOnGameStart()
    {
        deckRed.SetSwappedCard(TriadCards.None, -1);
        for (var slot = 0; slot < deckBlue.Cards.Length; slot++)
        {
            if (deckBlue.Cards[slot] == TriadCards.None)
            {
                return TriadScreenUpdate.None;
            }
        }

        if (hasRestartRule && IsSuddenDeathRestart())
        {
            return TriadScreenUpdate.None;
        }

        playerDeckPattern ??= (ushort[])deckBlue.Cards.Clone();
        if (blueDeckHistory.Count > MaxBlueHistory)
        {
            blueDeckHistory.RemoveAt(0);
        }

        blueDeckHistory.Add((ushort[])deckBlue.Cards.Clone());

        var swapped = FindSwappedCardVisible(out var blueIndex, out var redIndex, out var blueCard);
        if (!swapped && playerDeckPattern is not null)
        {
            swapped = FindSwappedCard(deckBlue.Cards, playerDeckPattern, out blueIndex, out redIndex, out blueCard);
            if (!swapped && FindCommonCards() is { } common)
            {
                swapped = FindSwappedCard(deckBlue.Cards, common, out blueIndex, out redIndex, out blueCard);
            }
        }

        if (!swapped)
        {
            SwappedBlueCardIndex = -1;
            return TriadScreenUpdate.SwapWarning;
        }

        deckRed.SetSwappedCard(blueCard, redIndex);
        SwappedBlueCardIndex = blueIndex;
        return TriadScreenUpdate.SwapHints;
    }

    private bool FindSwappedCard(ushort[] screenCards, ushort[] expectedCards, out int swappedIndex, out int otherIndex, out ushort swappedCard)
    {
        swappedIndex = -1;
        otherIndex = -1;
        swappedCard = TriadCards.None;
        var differences = 0;
        var potentialSwaps = 0;
        var length = Math.Min(screenCards.Length, expectedCards.Length);
        for (var slot = 0; slot < length; slot++)
        {
            var screenCard = screenCards[slot];
            if (screenCard == expectedCards[slot] || screenCard == TriadCards.None)
            {
                continue;
            }

            differences++;
            swappedIndex = slot;
            otherIndex = deckRed.GetCardIndex(screenCard);
            swappedCard = expectedCards[slot];
            if (otherIndex >= 0)
            {
                potentialSwaps++;
            }
        }

        return differences == 1 && potentialSwaps == 1;
    }

    // Faster path: a red card that is not in the NPC's deck must be the blue card that was traded away.
    private bool FindSwappedCardVisible(out int swappedIndex, out int otherIndex, out ushort swappedCard)
    {
        swappedIndex = -1;
        otherIndex = -1;
        swappedCard = TriadCards.None;
        var differences = 0;
        var onHand = 0;
        var npcDeck = deckRed.Deck!;
        var screenCards = deckBlue.Cards;
        for (var slot = 0; slot < deckRed.Cards.Length; slot++)
        {
            var redCard = deckRed.Cards[slot];
            if (redCard != TriadCards.None && redCard != TriadCards.Hidden && npcDeck.GetCardIndex(redCard) < 0)
            {
                otherIndex = slot;
                swappedCard = redCard;
                for (var screenSlot = 0; screenSlot < screenCards.Length; screenSlot++)
                {
                    if (npcDeck.GetCardIndex(screenCards[screenSlot]) >= 0)
                    {
                        swappedIndex = screenSlot;
                        differences++;
                    }
                }
            }

            onHand += redCard != TriadCards.None ? 1 : 0;
        }

        if (onHand < screenCards.Length)
        {
            var board = GameState.Board;
            for (var cell = 0; cell < board.Length; cell++)
            {
                if (board[cell].IsEmpty || board[cell].Owner != TriadOwner.Red || npcDeck.GetCardIndex(board[cell].CardId) >= 0)
                {
                    continue;
                }

                swappedCard = board[cell].CardId;
                // Past the hand and deck strip: the traded card has already been played to the board.
                otherIndex = 100;
                for (var screenSlot = 0; screenSlot < screenCards.Length; screenSlot++)
                {
                    if (npcDeck.GetCardIndex(screenCards[screenSlot]) >= 0)
                    {
                        swappedIndex = screenSlot;
                        differences++;
                    }
                }
            }
        }

        return differences == 1;
    }

    // Each slot's most frequent card over the hands seen so far, when it appeared at least twice.
    private ushort[]? FindCommonCards()
    {
        if (blueDeckHistory.Count <= 1)
        {
            return null;
        }

        var result = new ushort[blueDeckHistory[0].Length];
        var counts = new Dictionary<ushort, int>();
        for (var slot = 0; slot < result.Length; slot++)
        {
            counts.Clear();
            var bestCard = TriadCards.None;
            var bestCount = 0;
            for (var historyIndex = 0; historyIndex < blueDeckHistory.Count; historyIndex++)
            {
                var cardId = blueDeckHistory[historyIndex][slot];
                if (cardId == TriadCards.None)
                {
                    continue;
                }

                var count = counts.GetValueOrDefault(cardId) + 1;
                counts[cardId] = count;
                if (count > bestCount)
                {
                    bestCount = count;
                    bestCard = cardId;
                }
            }

            if (bestCount < 2)
            {
                return null;
            }

            result[slot] = bestCard;
        }

        return result;
    }

    // Four or more visible red cards without an open rule, or several that are not the NPC's, can only come from a
    // Sudden Death restart, where the hands are rebuilt from the board.
    private bool IsSuddenDeathRestart()
    {
        var mismatched = 0;
        var visible = 0;
        var npcDeck = deckRed.Deck!;
        for (var slot = 0; slot < deckRed.Cards.Length; slot++)
        {
            var cardId = deckRed.Cards[slot];
            if (npcDeck.GetCardIndex(cardId) < 0)
            {
                mismatched++;
            }

            if (cardId != TriadCards.None && cardId != TriadCards.Hidden)
            {
                visible++;
            }
        }

        return (visible >= 4 && mismatched > 1) || (visible >= 4 && !hasOpenRule);
    }
}
