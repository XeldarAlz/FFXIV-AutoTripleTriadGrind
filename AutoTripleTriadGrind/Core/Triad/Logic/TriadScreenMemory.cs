using AutoTripleTriadGrind.Core.Triad.Logic.Rules;

namespace AutoTripleTriadGrind.Core.Triad.Logic;

// What the board screen shows this frame: both hands, the board, the rules, and the card Chaos forces blue to play.
public sealed class TriadScreenState
{
    public readonly ushort[] BlueHand = TriadDeckInstanceScreen.NewHand();
    public readonly ushort[] RedHand = TriadDeckInstanceScreen.NewHand();
    public readonly ushort[] Board = NewBoardCards();
    public readonly TriadOwner[] BoardOwners = new TriadOwner[TriadGameState.BoardCells];
    public readonly List<TriadRule> Rules = new(4);
    public ushort ForcedBlueCard = TriadCards.None;
    public bool PlayerTurn;

    public static ushort[] NewBoardCards()
    {
        var cards = new ushort[TriadGameState.BoardCells];
        Array.Fill(cards, TriadCards.None);
        return cards;
    }
}

[Flags]
public enum TriadScreenUpdate : byte
{
    None = 0,
    Rules = 1 << 0,
    Board = 1 << 1,
    RedDeck = 1 << 2,
    BlueDeck = 1 << 3,
    SwapWarning = 1 << 4,
    SwapHints = 1 << 5,
}

// Keeps the solver's picture of a live match between screen reads: which NPC cards are still possible behind the
// face-down slots, and which card a Swap traded.
public sealed partial class TriadScreenMemory
{
    private const int MaxBlueHistory = 10;

    private readonly List<ushort[]> blueDeckHistory = [];
    private readonly TriadDeckInstanceScreen deckBlue = new();
    private readonly TriadDeckInstanceScreen deckRed = new();
    private bool hasOpenRule;
    private bool hasRestartRule;
    private bool hasSwapRule;
    private bool swapStartChecked;
    private TriadDeck? lastNpcDeck;
    private ushort[]? playerDeckPattern;

    public TriadScreenMemory()
    {
        GameState = new TriadGameState(deckBlue, deckRed);
    }

    public TriadSolver Solver { get; private set; } = TriadSolver.CreateLive();

    public TriadGameState GameState { get; }

    public int SwappedBlueCardIndex { get; private set; } = -1;

    public TriadDeckInstanceScreen DeckBlue => deckBlue;

    public TriadDeckInstanceScreen DeckRed => deckRed;

    public void UpdatePlayerDeck(TriadDeck playerDeck) => playerDeckPattern = [.. playerDeck.KnownCards];

    public void Reset()
    {
        lastNpcDeck = null;
        blueDeckHistory.Clear();
        swapStartChecked = false;
        SwappedBlueCardIndex = -1;
        playerDeckPattern = null;
        Array.Fill(GameState.Board, TriadBoardSlot.Empty);
        Solver = TriadSolver.CreateLive();
    }

    public TriadScreenUpdate OnNewScan(TriadScreenState screen, TriadDeck npcDeck)
    {
        var flags = TriadScreenUpdate.None;
        var continuesPrevious = ReferenceEquals(deckRed.Deck, npcDeck) && ReferenceEquals(lastNpcDeck, npcDeck);
        if (continuesPrevious)
        {
            for (var cell = 0; cell < TriadGameState.BoardCells; cell++)
            {
                if (!GameState.Board[cell].IsEmpty && screen.Board[cell] == TriadCards.None)
                {
                    continuesPrevious = false;
                }
            }
        }

        var simulation = Solver.Simulation;
        if (!SameRules(simulation.Rules, screen.Rules))
        {
            hasSwapRule = false;
            hasRestartRule = false;
            hasOpenRule = false;
            simulation.UseRules(screen.Rules);
            for (var index = 0; index < screen.Rules.Count; index++)
            {
                switch (screen.Rules[index].Id)
                {
                    case Data.TriadRuleId.Swap:
                        hasSwapRule = true;
                        break;
                    case Data.TriadRuleId.SuddenDeath:
                        hasRestartRule = true;
                        break;
                    case Data.TriadRuleId.AllOpen:
                        hasOpenRule = true;
                        break;
                }
            }

            flags |= TriadScreenUpdate.Rules;
            continuesPrevious = false;
            deckRed.SetSwappedCard(TriadCards.None, -1);
            Solver.Agent.OnSimulationStart();
            blueDeckHistory.Clear();
            swapStartChecked = false;
        }

        var npcChanged = !ReferenceEquals(lastNpcDeck, npcDeck);
        if (npcChanged)
        {
            blueDeckHistory.Clear();
            swapStartChecked = false;
        }

        if (npcChanged || !HandMatches(deckRed, screen.RedHand) || !ReferenceEquals(deckRed.Deck, npcDeck))
        {
            flags |= TriadScreenUpdate.RedDeck;
            deckRed.Deck = npcDeck;
            lastNpcDeck = npcDeck;
            if (npcChanged)
            {
                Solver.Agent.OnSimulationStart();
            }

            UpdateAvailableRedCards(screen.RedHand, screen.BlueHand, screen.Board, continuesPrevious);
        }

        if (!HandMatches(deckBlue, screen.BlueHand))
        {
            flags |= TriadScreenUpdate.BlueDeck;
            deckBlue.UpdateAvailableCards(screen.BlueHand);
        }

        GameState.Status = TriadGameStatus.InProgressBlue;
        GameState.DeckBlue = deckBlue;
        GameState.DeckRed = deckRed;
        GameState.NumCardsPlaced = 0;
        GameState.ForcedCardIndex = screen.ForcedBlueCard != TriadCards.None ? deckBlue.GetCardIndex(screen.ForcedBlueCard) : -1;
        if (GameState.ForcedCardIndex >= 0 && (deckBlue.AvailableCardMask & (1 << GameState.ForcedCardIndex)) == 0)
        {
            GameState.ForcedCardIndex = -1;
        }

        if (SyncBoard(screen))
        {
            flags |= TriadScreenUpdate.Board;
            for (var index = 0; index < simulation.Rules.Count; index++)
            {
                simulation.Rules[index].OnScreenUpdate(GameState);
            }
        }

        if (hasSwapRule && !swapStartChecked && GameState.NumCardsPlaced <= 1)
        {
            swapStartChecked = true;
            flags |= DetectSwapOnGameStart();
        }

        return flags;
    }

    private bool SyncBoard(TriadScreenState screen)
    {
        var changed = false;
        var board = GameState.Board;
        for (var cell = 0; cell < TriadGameState.BoardCells; cell++)
        {
            var screenCard = screen.Board[cell];
            var wasEmpty = board[cell].IsEmpty;
            var isEmpty = screenCard == TriadCards.None;
            if (wasEmpty && !isEmpty)
            {
                changed = true;
                board[cell] = new TriadBoardSlot { CardId = screenCard, Owner = screen.BoardOwners[cell] };
            }
            else if (!wasEmpty && isEmpty)
            {
                changed = true;
                board[cell] = TriadBoardSlot.Empty;
            }
            else if (!wasEmpty && (board[cell].Owner != screen.BoardOwners[cell] || board[cell].CardId != screenCard))
            {
                changed = true;
                board[cell] = new TriadBoardSlot { CardId = screenCard, Owner = screen.BoardOwners[cell] };
            }

            if (!board[cell].IsEmpty)
            {
                GameState.NumCardsPlaced++;
            }
        }

        return changed;
    }

    private static bool SameRules(List<TriadRule> current, List<TriadRule> screen)
    {
        if (current.Count != screen.Count)
        {
            return false;
        }

        for (var index = 0; index < current.Count; index++)
        {
            var found = false;
            for (var screenIndex = 0; screenIndex < screen.Count; screenIndex++)
            {
                if (screen[screenIndex].Id == current[index].Id)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HandMatches(TriadDeckInstanceScreen deck, ushort[] hand)
    {
        if (deck.Cards.Length < hand.Length)
        {
            return false;
        }

        for (var index = 0; index < hand.Length; index++)
        {
            if (hand[index] != deck.Cards[index])
            {
                return false;
            }
        }

        return true;
    }
}
