using AutoTripleTriadGrind.Core.Triad.Data;

namespace AutoTripleTriadGrind.Core.Triad.Logic;

// The solver reads card stats from one shared table so a card travels through the search as a two-byte id.
public static class TriadCards
{
    public const ushort None = ushort.MaxValue;
    public const ushort Hidden = 0;
    public const int MinNumber = 1;
    public const int MaxNumber = 10;

    private static TriadCard[] table = [];

    public static ReadOnlySpan<TriadCard> Table => table;

    public static void Use(TriadCard[] cards) => table = cards;

    public static ref readonly TriadCard Get(ushort cardId) => ref table[cardId];

    public static bool IsPlayable(ushort cardId) => cardId != None && cardId != Hidden && cardId < table.Length && table[cardId].IsValid;

    public static int Number(ushort cardId, int side) => table[cardId].Side(side);
}

public enum TriadOwner : byte
{
    Unknown,
    Blue,
    Red,
}

public enum TriadGameStatus : byte
{
    InProgressBlue,
    InProgressRed,
    BlueWins,
    BlueDraw,
    BlueLost,
}

[Flags]
public enum TriadSpecialRules : byte
{
    None = 0,
    SelectVisible3 = 1 << 0,
    SelectVisible5 = 1 << 1,
    RandomizeRule = 1 << 2,
    RandomizeBlueDeck = 1 << 3,
    SwapCards = 1 << 4,
    BlueCardSelection = 1 << 5,
    IgnoreOwnedCheck = 1 << 6,
}

public struct TriadBoardSlot
{
    public ushort CardId;
    public TriadOwner Owner;
    public sbyte ScoreModifier;

    public static readonly TriadBoardSlot Empty = new() { CardId = TriadCards.None };

    public readonly bool IsEmpty => CardId == TriadCards.None;

    public readonly int RawNumber(int side) => TriadCards.Number(CardId, side);

    public readonly int Number(int side) => Math.Clamp(RawNumber(side) + ScoreModifier, TriadCards.MinNumber, TriadCards.MaxNumber);

    public readonly int OppositeNumber(int side) => Number(TriadSide.Opposite(side));

    public readonly TriadCardType Type => TriadCards.Get(CardId).Type;
}
