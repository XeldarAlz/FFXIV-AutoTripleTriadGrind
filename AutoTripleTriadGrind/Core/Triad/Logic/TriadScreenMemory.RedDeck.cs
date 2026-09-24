namespace AutoTripleTriadGrind.Core.Triad.Logic;

public sealed partial class TriadScreenMemory
{
    // The five visible red slots sit at mask bits 0-4 and the NPC's whole deck follows from bit 5, known cards first
    // and then the variable pool. Cards seen in hand or played on the board are crossed off the deck bits, so the
    // face-down slots are modelled by whatever the NPC could still be holding.
    private void UpdateAvailableRedCards(ushort[] screenRed, ushort[] screenBlue, ushort[] screenBoard, bool continuesPrevious)
    {
        var npcDeck = deckRed.Deck!;
        var visibleCount = deckRed.Cards.Length;
        deckRed.NumPlaced = 0;
        if (!continuesPrevious)
        {
            deckRed.NumUnknownPlaced = 0;
        }

        var knownCount = npcDeck.KnownCards.Count;
        var maxUnknownToUse = visibleCount - knownCount;
        var firstUnknownPoolIndex = visibleCount + knownCount;
        if (npcDeck.UnknownPool.Count > 0)
        {
            deckRed.UnknownPoolMask = ((1 << npcDeck.UnknownPool.Count) - 1) << firstUnknownPoolIndex;
            for (var slot = 0; slot < screenRed.Length; slot++)
            {
                var cardId = screenRed[slot];
                if (cardId != TriadCards.None && cardId != TriadCards.Hidden && npcDeck.UnknownPool.Contains(cardId))
                {
                    deckRed.UnknownPoolMask |= 1 << slot;
                }
            }
        }

        var allDeckMask = ((1 << npcDeck.CardCount) - 1) << visibleCount;
        var previousBoard = GameState.Board;
        var canCompare = true;
        var freshStart = false;
        if (!continuesPrevious)
        {
            var cardsOnBoard = 0;
            for (var cell = 0; cell < screenBoard.Length; cell++)
            {
                if (screenBoard[cell] != TriadCards.None)
                {
                    cardsOnBoard++;
                }
            }

            if (cardsOnBoard <= 1)
            {
                freshStart = true;
                deckRed.Cards = TriadDeckInstanceScreen.NewHand();
                deckRed.AvailableCardMask = allDeckMask;
                deckRed.NumPlaced = 0;
                deckRed.NumUnknownPlaced = 0;
            }
            else
            {
                canCompare = false;
            }
        }

        if (!canCompare)
        {
            deckRed.UpdateAvailableCards(screenRed);
            deckRed.AvailableCardMask = allDeckMask;
            return;
        }

        var usedIndices = new List<int>(TriadDeck.HandSize);
        var usedByBlue = new List<ushort>(TriadDeck.HandSize);
        var knownOnHand = 0;
        var unknownOnHand = 0;
        var hidden = 0;
        var onHand = 0;
        for (var slot = 0; slot < visibleCount; slot++)
        {
            var screenCard = screenRed[slot];
            if (screenCard == TriadCards.None)
            {
                var previous = deckRed.Cards[slot];
                if (previous != TriadCards.None && previous != TriadCards.Hidden)
                {
                    usedIndices.Add(slot);
                }

                deckRed.AvailableCardMask &= ~(1 << slot);
                deckRed.NumPlaced++;
                continue;
            }

            if (screenCard == TriadCards.Hidden)
            {
                // A face-down slot is never playable itself; the deck bits behind it stand for it.
                hidden++;
                continue;
            }

            var unknown = (deckRed.UnknownPoolMask & (1 << slot)) != 0;
            unknownOnHand += unknown ? 1 : 0;
            knownOnHand += unknown ? 0 : 1;
            onHand++;
            deckRed.AvailableCardMask |= 1 << slot;
            var knownIndex = npcDeck.KnownCards.IndexOf(screenCard);
            var poolIndex = npcDeck.UnknownPool.IndexOf(screenCard);
            if (knownIndex >= 0)
            {
                deckRed.AvailableCardMask &= ~(1 << (knownIndex + visibleCount));
            }
            else if (poolIndex >= 0)
            {
                deckRed.AvailableCardMask &= ~(1 << (poolIndex + visibleCount + knownCount));
            }
        }

        // Blue's hand is read before this scan updates it, so it still shows what blue held last frame.
        var comparedBlue = freshStart ? TriadDeckInstanceScreen.NewHand() : deckBlue.Cards;
        for (var slot = 0; slot < comparedBlue.Length; slot++)
        {
            if (comparedBlue[slot] != TriadCards.None && screenBlue[slot] == TriadCards.None)
            {
                usedByBlue.Add(comparedBlue[slot]);
            }
        }

        for (var cell = 0; cell < screenBoard.Length; cell++)
        {
            var screenCard = screenBoard[cell];
            var wasEmpty = freshStart || previousBoard[cell].IsEmpty;
            if (!wasEmpty || screenCard == TriadCards.None)
            {
                continue;
            }

            var cardIndex = deckRed.GetCardIndex(screenCard);
            if (!usedByBlue.Contains(screenCard) && cardIndex >= 0)
            {
                usedIndices.Add(cardIndex);
            }
        }

        screenRed.AsSpan(0, visibleCount).CopyTo(deckRed.Cards);
        for (var index = 0; index < usedIndices.Count; index++)
        {
            var cardMask = 1 << usedIndices[index];
            deckRed.AvailableCardMask &= ~cardMask;
            if ((deckRed.UnknownPoolMask & cardMask) != 0)
            {
                deckRed.NumUnknownPlaced++;
            }
        }

        if (hidden == 0 && onHand + deckRed.NumPlaced == visibleCount)
        {
            deckRed.AvailableCardMask &= (1 << visibleCount) - 1;
        }
        else if (deckRed.NumUnknownPlaced + unknownOnHand >= maxUnknownToUse || (knownOnHand >= visibleCount - maxUnknownToUse && hidden == 0))
        {
            deckRed.AvailableCardMask &= (1 << (visibleCount + knownCount)) - 1;
        }
    }
}
