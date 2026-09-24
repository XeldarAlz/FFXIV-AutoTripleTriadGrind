using AutoTripleTriadGrind.Core.Triad.Data;
using AutoTripleTriadGrind.Core.Triad.Logic;

namespace AutoTripleTriadGrind.Tests;

// A small card table with hand-picked sides, so each rule can be driven into a known capture.
internal static class TestCards
{
    public const ushort Weak = 1;
    public const ushort Strong = 2;
    public const ushort AllFives = 3;
    public const ushort AllSixes = 4;
    public const ushort Ace = 5;
    public const ushort One = 6;
    public const ushort PrimalLow = 7;
    public const ushort PrimalHigh = 8;
    public const ushort Twos = 9;
    public const ushort Fours = 10;
    public const ushort Threes = 11;
    public const ushort Sevens = 12;
    public const ushort Eights = 13;
    public const ushort Nines = 14;
    public const ushort Mixed = 15;
    public const int Count = 16;

    public static void Install()
    {
        var cards = new TriadCard[Count];
        cards[Weak] = Card(Weak, 1, 1, 1, 1);
        cards[Strong] = Card(Strong, 9, 9, 9, 9, stars: 3);
        cards[AllFives] = Card(AllFives, 5, 5, 5, 5);
        cards[AllSixes] = Card(AllSixes, 6, 6, 6, 6);
        cards[Ace] = Card(Ace, 10, 10, 10, 10, stars: 5);
        cards[One] = Card(One, 1, 1, 1, 1);
        cards[PrimalLow] = Card(PrimalLow, 4, 4, 4, 4, TriadCardType.Primal);
        cards[PrimalHigh] = Card(PrimalHigh, 5, 5, 5, 5, TriadCardType.Primal);
        cards[Twos] = Card(Twos, 2, 2, 2, 2);
        cards[Fours] = Card(Fours, 4, 4, 4, 4);
        cards[Threes] = Card(Threes, 3, 3, 3, 3);
        cards[Sevens] = Card(Sevens, 7, 7, 7, 7, stars: 2);
        cards[Eights] = Card(Eights, 8, 8, 8, 8, stars: 3);
        cards[Nines] = Card(Nines, 9, 9, 9, 9, stars: 4);
        cards[Mixed] = Card(Mixed, 7, 3, 7, 3, stars: 2);
        TriadCards.Use(cards);
    }

    public static TriadCard Card(ushort id, byte up, byte right, byte down, byte left, TriadCardType type = TriadCardType.None, byte stars = 1)
        => new(id, up, right, down, left, type, stars, id, 0);

    public static TriadGameState Game(TriadSimulation simulation, ushort[] blue, ushort[] red, TriadGameStatus opener = TriadGameStatus.InProgressBlue)
        => simulation.StartGame(new TriadDeck(blue), new TriadDeck(red), opener);
}
