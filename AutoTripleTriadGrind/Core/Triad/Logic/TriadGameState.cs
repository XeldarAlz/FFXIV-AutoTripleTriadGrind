using AutoTripleTriadGrind.Core.Triad.Data;

namespace AutoTripleTriadGrind.Core.Triad.Logic;

public sealed class TriadGameState
{
    public const int BoardSize = 3;
    public const int BoardCells = BoardSize * BoardSize;
    private const int CardTypes = 5;

    public readonly TriadBoardSlot[] Board = NewBoard();
    public readonly int[] TypeModifiers = new int[CardTypes];
    public TriadDeckInstance DeckBlue;
    public TriadDeckInstance DeckRed;
    public int ForcedCardIndex = -1;
    public int NumCardsPlaced;
    public int NumRestarts;
    public TriadGameStatus Status = TriadGameStatus.InProgressBlue;

    public TriadGameState(TriadDeckInstance deckBlue, TriadDeckInstance deckRed)
    {
        DeckBlue = deckBlue;
        DeckRed = deckRed;
    }

    public TriadGameState(TriadGameState copyFrom)
    {
        Array.Copy(copyFrom.Board, Board, BoardCells);
        Array.Copy(copyFrom.TypeModifiers, TypeModifiers, CardTypes);
        DeckBlue = copyFrom.DeckBlue.CreateCopy();
        DeckRed = copyFrom.DeckRed.CreateCopy();
        Status = copyFrom.Status;
        NumCardsPlaced = copyFrom.NumCardsPlaced;
        NumRestarts = copyFrom.NumRestarts;
        ForcedCardIndex = copyFrom.ForcedCardIndex;
    }

    public bool IsFinished => Status is TriadGameStatus.BlueWins or TriadGameStatus.BlueDraw or TriadGameStatus.BlueLost;

    public TriadDeckInstance TurnDeck => Status == TriadGameStatus.InProgressBlue ? DeckBlue : DeckRed;

    public TriadOwner TurnOwner => Status == TriadGameStatus.InProgressBlue ? TriadOwner.Blue : TriadOwner.Red;

    public void ClearTypeModifiers() => Array.Clear(TypeModifiers);

    public int TypeModifier(TriadCardType type) => TypeModifiers[(int)type];

    public static TriadBoardSlot[] NewBoard()
    {
        var board = new TriadBoardSlot[BoardCells];
        Array.Fill(board, TriadBoardSlot.Empty);
        return board;
    }
}
