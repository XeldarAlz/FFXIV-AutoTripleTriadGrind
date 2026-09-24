using AutoTripleTriadGrind.Core.Triad.Data;
using AutoTripleTriadGrind.Core.Triad.Logic.Rules;

namespace AutoTripleTriadGrind.Core.Triad.Logic;

// One instance per thread: the combo lists are reused between placements, so a simulation must not be shared.
public sealed class TriadSimulation
{
    private static readonly int[] neighborTable = BuildNeighborTable();

    private readonly List<int> comboList = new(TriadGameState.BoardCells);
    private readonly List<int> nextComboList = new(TriadGameState.BoardCells);

    public readonly List<TriadRule> Rules = new(4);

    public TriadRuleFeatures Features { get; private set; }

    public TriadSpecialRules SpecialRules { get; private set; }

    public void Initialize(IReadOnlyList<TriadRule> primary, IReadOnlyList<TriadRule>? secondary = null)
    {
        Rules.Clear();
        AddCopies(primary);
        if (secondary is not null)
        {
            AddCopies(secondary);
        }

        UpdateSpecialRules();
    }

    public void CopyRulesFrom(TriadSimulation source)
    {
        Rules.Clear();
        for (var index = 0; index < source.Rules.Count; index++)
        {
            Rules.Add(source.Rules[index].Clone());
        }

        UpdateSpecialRules();
    }

    public void UseRules(IReadOnlyList<TriadRule> rules)
    {
        Rules.Clear();
        for (var index = 0; index < rules.Count; index++)
        {
            Rules.Add(rules[index]);
        }

        UpdateSpecialRules();
    }

    public void UpdateSpecialRules()
    {
        SpecialRules = TriadSpecialRules.None;
        Features = TriadRuleFeatures.None;
        for (var index = 0; index < Rules.Count; index++)
        {
            SpecialRules |= Rules[index].SpecialRules;
            Features |= Rules[index].Features;
        }
    }

    public bool HasSpecialRule(TriadSpecialRules rule) => (SpecialRules & rule) != 0;

    public bool HasFeature(TriadRuleFeatures feature) => (Features & feature) != 0;

    public TriadGameState StartGame(TriadDeck deckBlue, TriadDeck deckRed, TriadGameStatus status)
    {
        for (var index = 0; index < Rules.Count; index++)
        {
            Rules[index].OnMatchInit();
        }

        return new TriadGameState(new TriadDeckInstanceManual(deckBlue), new TriadDeckInstanceManual(deckRed)) { Status = status };
    }

    public static ReadOnlySpan<int> Neighbors(int boardPosition) => neighborTable.AsSpan(boardPosition * TriadSide.Count, TriadSide.Count);

    public bool PlaceCard(TriadGameState state, int cardIndex, TriadDeckInstance deck, TriadOwner owner, int boardPosition)
    {
        var allowedOwner = (owner == TriadOwner.Blue && state.Status == TriadGameStatus.InProgressBlue)
            || (owner == TriadOwner.Red && state.Status == TriadGameStatus.InProgressRed);
        var cardId = deck.GetCard(cardIndex);
        if (!allowedOwner || (uint)boardPosition >= TriadGameState.BoardCells || !state.Board[boardPosition].IsEmpty || cardId == TriadCards.None)
        {
            return false;
        }

        state.Board[boardPosition] = new TriadBoardSlot { CardId = cardId, Owner = owner };
        state.NumCardsPlaced++;
        if (owner == TriadOwner.Blue)
        {
            state.DeckBlue.OnCardPlaced(cardIndex);
            state.Status = TriadGameStatus.InProgressRed;
        }
        else
        {
            state.DeckRed.OnCardPlaced(cardIndex);
            state.Status = TriadGameStatus.InProgressBlue;
        }

        var placed = owner == TriadOwner.Red || !HasSpecialRule(TriadSpecialRules.IgnoreOwnedCheck);
        var allowCombo = false;
        if (HasFeature(TriadRuleFeatures.CardPlaced))
        {
            for (var index = 0; index < Rules.Count; index++)
            {
                Rules[index].OnCardPlaced(state, boardPosition);
                allowCombo |= Rules[index].AllowsCombo;
            }
        }

        comboList.Clear();
        CheckCaptures(state, boardPosition, comboList, 0);
        var comboCounter = 0;
        var current = comboList;
        var next = nextComboList;
        while (allowCombo && current.Count > 0)
        {
            next.Clear();
            comboCounter++;
            for (var index = 0; index < current.Count; index++)
            {
                CheckCaptures(state, current[index], next, comboCounter);
            }

            (current, next) = (next, current);
        }

        if (HasFeature(TriadRuleFeatures.PostCapture))
        {
            for (var index = 0; index < Rules.Count; index++)
            {
                Rules[index].OnPostCaptures(state, boardPosition);
            }
        }

        if (state.NumCardsPlaced == TriadGameState.BoardCells)
        {
            OnAllCardsPlaced(state);
        }

        return placed;
    }

    public bool PlaceCard(TriadGameState state, ushort cardId, TriadOwner owner, int boardPosition)
    {
        var deck = owner == TriadOwner.Blue ? state.DeckBlue : state.DeckRed;
        return PlaceCard(state, deck.GetCardIndex(cardId), deck, owner, boardPosition);
    }

    // Rule captures on the placed card only count on the first step; a combo step compares plain numbers.
    private void CheckCaptures(TriadGameState state, int boardPosition, List<int> combo, int comboCounter)
    {
        var neighbors = Neighbors(boardPosition);
        var allowRules = comboCounter == 0;
        if (allowRules && HasFeature(TriadRuleFeatures.CaptureNeighbors))
        {
            for (var index = 0; index < Rules.Count; index++)
            {
                Rules[index].OnCheckCaptureNeighbors(state, boardPosition, neighbors, combo);
            }
        }

        var reverseActive = allowRules && HasFeature(TriadRuleFeatures.CaptureMath);
        var weights = allowRules && HasFeature(TriadRuleFeatures.CaptureWeights);
        var board = state.Board;
        var checkOwner = board[boardPosition].Owner;
        for (var side = 0; side < TriadSide.Count; side++)
        {
            var neighbor = neighbors[side];
            if (neighbor < 0 || board[neighbor].IsEmpty || board[neighbor].Owner == checkOwner)
            {
                continue;
            }

            var cardNumber = board[boardPosition].Number(side);
            var neighborNumber = board[neighbor].OppositeNumber(side);
            if (weights)
            {
                for (var index = 0; index < Rules.Count; index++)
                {
                    Rules[index].OnCheckCaptureWeights(state, boardPosition, neighbor, reverseActive, ref cardNumber, ref neighborNumber);
                }
            }

            var captured = cardNumber > neighborNumber;
            if (reverseActive)
            {
                for (var index = 0; index < Rules.Count; index++)
                {
                    Rules[index].OnCheckCaptureMath(state, boardPosition, neighbor, cardNumber, neighborNumber, ref captured);
                }
            }

            if (!captured)
            {
                continue;
            }

            board[neighbor].Owner = checkOwner;
            if (comboCounter > 0)
            {
                combo.Add(neighbor);
            }
        }
    }

    // A card still in blue's hand counts toward blue's total, as the game scores it.
    private void OnAllCardsPlaced(TriadGameState state)
    {
        var blueCount = state.DeckBlue.AvailableCardMask != 0 ? 1 : 0;
        var board = state.Board;
        for (var cell = 0; cell < board.Length; cell++)
        {
            if (board[cell].Owner == TriadOwner.Blue)
            {
                blueCount++;
            }
        }

        const int blueToWin = TriadGameState.BoardCells / 2 + 1;
        state.Status = blueCount > blueToWin ? TriadGameStatus.BlueWins
            : blueCount == blueToWin ? TriadGameStatus.BlueDraw
            : TriadGameStatus.BlueLost;

        if (!HasFeature(TriadRuleFeatures.AllPlaced))
        {
            return;
        }

        for (var index = 0; index < Rules.Count; index++)
        {
            Rules[index].OnAllCardsPlaced(state);
        }
    }

    private void AddCopies(IReadOnlyList<TriadRule> rules)
    {
        for (var index = 0; index < rules.Count; index++)
        {
            Rules.Add(TriadRules.Create(rules[index].Id));
        }
    }

    // Upstream geometry: Right is x + 1 and Left is x - 1, matching the sides the card data calls Right and Left.
    private static int[] BuildNeighborTable()
    {
        var table = new int[TriadGameState.BoardCells * TriadSide.Count];
        for (var position = 0; position < TriadGameState.BoardCells; position++)
        {
            var x = position % TriadGameState.BoardSize;
            var y = position / TriadGameState.BoardSize;
            var baseIndex = position * TriadSide.Count;
            table[baseIndex + TriadSide.Up] = y > 0 ? position - TriadGameState.BoardSize : -1;
            table[baseIndex + TriadSide.Down] = y < TriadGameState.BoardSize - 1 ? position + TriadGameState.BoardSize : -1;
            table[baseIndex + TriadSide.Right] = x < TriadGameState.BoardSize - 1 ? position + 1 : -1;
            table[baseIndex + TriadSide.Left] = x > 0 ? position - 1 : -1;
        }

        return table;
    }
}
