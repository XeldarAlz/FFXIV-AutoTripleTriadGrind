using AutoTripleTriadGrind.Core.Triad.Logic;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace AutoTripleTriadGrind.Core.Triad.Addons;

// The saved Triple Triad decks live in the Gold Saucer module; writing through it marks the file for the game to save.
internal static unsafe class ProfileDecks
{
    public const int SlotCount = 5;
    public const string DeckName = "Triad Grind";

    public static bool TryRead(int slot, Span<ushort> cards)
    {
        var module = GoldSaucerModule.Instance();
        if (module is null || (uint)slot >= SlotCount)
        {
            return false;
        }

        var deck = module->GetDeck(slot);
        if (deck is null)
        {
            return false;
        }

        var saved = deck->Cards;
        for (var index = 0; index < TriadDeck.HandSize; index++)
        {
            cards[index] = saved[index];
        }

        return true;
    }

    public static bool HasCards(int slot)
    {
        Span<ushort> cards = stackalloc ushort[TriadDeck.HandSize];
        if (!TryRead(slot, cards))
        {
            return false;
        }

        for (var index = 0; index < cards.Length; index++)
        {
            if (!TriadCards.IsPlayable(cards[index]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryWrite(int slot, ReadOnlySpan<ushort> cards)
    {
        var module = GoldSaucerModule.Instance();
        if (module is null || (uint)slot >= SlotCount || cards.Length != TriadDeck.HandSize)
        {
            return false;
        }

        for (var index = 0; index < cards.Length; index++)
        {
            if (!TriadCards.IsPlayable(cards[index]))
            {
                return false;
            }
        }

        for (var index = 0; index < cards.Length; index++)
        {
            module->SetDeckCard(slot, index, cards[index]);
        }

        module->SetDeckName(slot, DeckName);
        return true;
    }
}
