using AutoTripleTriadGrind.Core.Triad.Logic.Agents;
using AutoTripleTriadGrind.Core.Triad.Logic.Rules;
using System.Numerics;

namespace AutoTripleTriadGrind.Core.Triad.Logic;

public readonly struct TriadSolverResult
{
    public static readonly TriadSolverResult Zero = new(0f, 0f, 0);

    public readonly float Wins;
    public readonly float Draws;
    public readonly long Games;
    public readonly float WinChance;
    public readonly float DrawChance;
    public readonly TriadGameStatus Expected;
    public readonly float Score;

    // A likely win always outranks a likely draw, which outranks a likely loss; within a band the chance decides.
    public TriadSolverResult(float wins, float draws, long games)
    {
        Wins = wins;
        Draws = draws;
        Games = games;
        WinChance = games <= 0 ? 0f : wins / games;
        DrawChance = games <= 0 ? 0f : draws / games;
        if (WinChance < 0.25f && DrawChance < 0.25f)
        {
            Score = WinChance / 10f;
            Expected = TriadGameStatus.BlueLost;
        }
        else if (WinChance < DrawChance)
        {
            Score = DrawChance;
            Expected = TriadGameStatus.BlueDraw;
        }
        else
        {
            Score = WinChance + 10f;
            Expected = TriadGameStatus.BlueWins;
        }
    }

    public bool IsBetterThan(in TriadSolverResult other) => Score > other.Score;
}

public sealed class TriadSolver
{
    public readonly TriadSimulation Simulation = new();

    public TriadSolver(TriadAgent agent)
    {
        Agent = agent;
        Agent.Initialize(0);
    }

    public TriadAgent Agent { get; }

    public static TriadSolver CreateLive() => new(new TriadLiveAgent());

    // Rules keep per-match state, so every parallel worker gets its own copies.
    public TriadSolver CreateWorkerCopy()
    {
        var worker = new TriadSolver(new TriadRandomAgent(0));
        worker.Simulation.CopyRulesFrom(Simulation);
        return worker;
    }

    public void InitializeSimulation(IReadOnlyList<TriadRule> primary, IReadOnlyList<TriadRule>? secondary = null) => Simulation.Initialize(primary, secondary);

    public TriadGameState StartSimulation(TriadDeck deckBlue, TriadDeck deckRed, TriadGameStatus status)
    {
        Agent.OnSimulationStart();
        return Simulation.StartGame(deckBlue, deckRed, status);
    }

    public bool FindNextMove(TriadGameState state, out int cardIndex, out int boardPosition, out TriadSolverResult result)
        => Agent.FindNextMove(this, state, out cardIndex, out boardPosition, out result);

    public void RunSimulation(TriadGameState state, TriadAgent blue, TriadAgent red)
    {
        while (true)
        {
            TriadAgent agent;
            TriadDeckInstance deck;
            TriadOwner owner;
            if (state.Status == TriadGameStatus.InProgressBlue)
            {
                agent = blue;
                deck = state.DeckBlue;
                owner = TriadOwner.Blue;
            }
            else if (state.Status == TriadGameStatus.InProgressRed)
            {
                agent = red;
                deck = state.DeckRed;
                owner = TriadOwner.Red;
            }
            else
            {
                return;
            }

            if (!agent.FindNextMove(this, state, out var cardIndex, out var boardPosition, out _))
            {
                return;
            }

            if (!Simulation.PlaceCard(state, cardIndex, deck, owner, boardPosition))
            {
                return;
            }
        }
    }

    public void FindAvailableActions(TriadGameState state, out int boardMask, out int cardsMask)
    {
        boardMask = 0;
        var board = state.Board;
        for (var cell = 0; cell < board.Length; cell++)
        {
            if (board[cell].IsEmpty)
            {
                boardMask |= 1 << cell;
            }
        }

        var deckMask = state.TurnDeck.AvailableCardMask;
        if (state.ForcedCardIndex >= 0)
        {
            var forced = 1 << state.ForcedCardIndex;
            cardsMask = (deckMask & forced) != 0 ? forced : deckMask;
        }
        else
        {
            cardsMask = deckMask;
        }

        if (!Simulation.HasFeature(TriadRuleFeatures.FilterNext))
        {
            return;
        }

        var rules = Simulation.Rules;
        for (var index = 0; index < rules.Count; index++)
        {
            rules[index].OnFilterNextCards(state, ref cardsMask);
        }
    }

    public void FindAvailableActions(TriadGameState state, out int boardMask, out int boardCount, out int cardsMask, out int cardsCount)
    {
        FindAvailableActions(state, out boardMask, out cardsMask);
        boardCount = BitOperations.PopCount((uint)boardMask);
        cardsCount = BitOperations.PopCount((uint)cardsMask);
    }
}
