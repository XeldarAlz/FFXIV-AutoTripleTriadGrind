namespace AutoTripleTriadGrind.Core.Triad.Logic;

// Card indices address the deck as one strip: bit N of AvailableCardMask is card N, so a whole hand fits in an int.
public abstract class TriadDeckInstance
{
    public const int MaxAvailableCards = 15;
    // Five cards win a board of nine, so a player never places more than this many.
    public const int MaxCardsToPlace = TriadGameState.BoardCells / 2 + 1;

    public int AvailableCardMask;
    public TriadDeck? Deck;
    public int NumPlaced;
    public int NumUnknownPlaced;

    public abstract void OnCardPlaced(int cardIndex);

    public abstract int FirstAvailableCard();

    public abstract ushort GetCard(int cardIndex);

    public abstract int GetCardIndex(ushort cardId);

    public abstract TriadDeckInstance CreateCopy();

    public bool IsPlaced(int cardIndex) => (AvailableCardMask & (1 << cardIndex)) == 0;

    public ushort FirstAvailableCardId()
    {
        var cardIndex = FirstAvailableCard();
        return cardIndex < 0 ? TriadCards.None : GetCard(cardIndex);
    }
}

public sealed class TriadDeckInstanceManual : TriadDeckInstance
{
    private readonly TriadDeck deck;

    public TriadDeckInstanceManual(TriadDeck deck)
    {
        this.deck = deck;
        Deck = deck;
        AvailableCardMask = (1 << deck.CardCount) - 1;
    }

    private TriadDeckInstanceManual(TriadDeckInstanceManual copyFrom)
    {
        deck = copyFrom.deck;
        Deck = copyFrom.deck;
        NumUnknownPlaced = copyFrom.NumUnknownPlaced;
        NumPlaced = copyFrom.NumPlaced;
        AvailableCardMask = copyFrom.AvailableCardMask;
    }

    public TriadDeck Source => deck;

    public override TriadDeckInstance CreateCopy() => new TriadDeckInstanceManual(this);

    // Once the NPC has drawn every card its hand leaves room for, the rest of the unknown pool drops out.
    public override void OnCardPlaced(int cardIndex)
    {
        AvailableCardMask &= ~(1 << cardIndex);
        NumPlaced++;
        var knownCount = deck.KnownCards.Count;
        if (cardIndex < knownCount)
        {
            return;
        }

        NumUnknownPlaced++;
        if (NumUnknownPlaced >= MaxCardsToPlace - knownCount)
        {
            AvailableCardMask &= (1 << knownCount) - 1;
        }
    }

    public override int FirstAvailableCard()
    {
        var knownCount = deck.KnownCards.Count;
        for (var cardIndex = 0; cardIndex < knownCount; cardIndex++)
        {
            if ((AvailableCardMask & (1 << cardIndex)) != 0)
            {
                return cardIndex;
            }
        }

        return -1;
    }

    public override ushort GetCard(int cardIndex) => deck.GetCard(cardIndex);

    public override int GetCardIndex(ushort cardId)
    {
        var cardIndex = deck.KnownCards.IndexOf(cardId);
        return cardIndex >= 0 ? cardIndex : deck.UnknownPool.IndexOf(cardId) + deck.KnownCards.Count;
    }
}

// The hand as the board screen shows it: the five visible slots come first, then the NPC's whole deck behind them,
// so a face-down card can still be reasoned about as any card the NPC might hold.
public sealed class TriadDeckInstanceScreen : TriadDeckInstance
{
    public ushort[] Cards = NewHand();
    public ushort SwappedCard = TriadCards.None;
    public int SwappedCardIndex = -1;
    public int UnknownPoolMask;

    public TriadDeckInstanceScreen()
    {
    }

    private TriadDeckInstanceScreen(TriadDeckInstanceScreen copyFrom)
    {
        Cards = (ushort[])copyFrom.Cards.Clone();
        Deck = copyFrom.Deck;
        NumUnknownPlaced = copyFrom.NumUnknownPlaced;
        NumPlaced = copyFrom.NumPlaced;
        AvailableCardMask = copyFrom.AvailableCardMask;
        UnknownPoolMask = copyFrom.UnknownPoolMask;
        SwappedCardIndex = copyFrom.SwappedCardIndex;
        SwappedCard = copyFrom.SwappedCard;
    }

    public static ushort[] NewHand()
    {
        var hand = new ushort[TriadDeck.HandSize];
        Array.Fill(hand, TriadCards.None);
        return hand;
    }

    public override TriadDeckInstance CreateCopy() => new TriadDeckInstanceScreen(this);

    public override int FirstAvailableCard()
    {
        for (var cardIndex = 0; cardIndex < Cards.Length; cardIndex++)
        {
            if ((AvailableCardMask & (1 << cardIndex)) != 0)
            {
                return cardIndex;
            }
        }

        return -1;
    }

    public void UpdateAvailableCards(ReadOnlySpan<ushort> screenCards)
    {
        AvailableCardMask = 0;
        NumPlaced = 0;
        NumUnknownPlaced = 0;
        screenCards[..Cards.Length].CopyTo(Cards);
        for (var cardIndex = 0; cardIndex < Cards.Length; cardIndex++)
        {
            var cardId = Cards[cardIndex];
            if (cardId == TriadCards.None)
            {
                NumPlaced++;
                continue;
            }

            if (cardId != TriadCards.Hidden)
            {
                AvailableCardMask |= 1 << cardIndex;
            }
        }
    }

    public void SetSwappedCard(ushort swappedCard, int swappedCardIndex)
    {
        SwappedCard = swappedCard;
        SwappedCardIndex = swappedCardIndex;
        if (swappedCardIndex >= 0)
        {
            UnknownPoolMask &= ~(1 << swappedCardIndex);
        }
    }

    public override void OnCardPlaced(int cardIndex)
    {
        var cardMask = 1 << cardIndex;
        AvailableCardMask &= ~cardMask;
        if (Deck is null || (UnknownPoolMask & cardMask) == 0)
        {
            return;
        }

        NumUnknownPlaced++;
        if (NumUnknownPlaced >= Cards.Length - Deck.KnownCards.Count)
        {
            AvailableCardMask &= ~UnknownPoolMask;
        }
    }

    public override ushort GetCard(int cardIndex)
    {
        if (cardIndex < 0)
        {
            return TriadCards.None;
        }

        if (cardIndex == SwappedCardIndex)
        {
            return SwappedCard;
        }

        if (cardIndex < Cards.Length)
        {
            return Cards[cardIndex];
        }

        return Deck?.GetCard(cardIndex - Cards.Length) ?? TriadCards.None;
    }

    public override int GetCardIndex(ushort cardId)
    {
        if (cardId == TriadCards.None)
        {
            return -1;
        }

        if (cardId == SwappedCard)
        {
            return SwappedCardIndex;
        }

        var cardIndex = Array.IndexOf(Cards, cardId);
        if (cardIndex >= 0 || Deck is null)
        {
            return cardIndex;
        }

        cardIndex = Deck.GetCardIndex(cardId);
        return cardIndex >= 0 ? cardIndex + Cards.Length : -1;
    }
}
