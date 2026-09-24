using System.Threading;
using System.Threading.Tasks;

namespace AutoTripleTriadGrind.Core.Triad.Logic.Agents;

public abstract class TriadAgent
{
    protected int SessionSeed;

    public virtual void Initialize(int sessionSeed) => SessionSeed = sessionSeed;

    public virtual void OnSimulationStart()
    {
    }

    public abstract bool FindNextMove(TriadSolver solver, TriadGameState state, out int cardIndex, out int boardPosition, out TriadSolverResult result);

    protected static bool IsFinished(TriadGameState state, out TriadSolverResult result)
    {
        switch (state.Status)
        {
            case TriadGameStatus.BlueWins:
                result = new TriadSolverResult(1f, 0f, 1);
                return true;
            case TriadGameStatus.BlueDraw:
                result = new TriadSolverResult(0f, 1f, 1);
                return true;
            case TriadGameStatus.BlueLost:
                result = new TriadSolverResult(0f, 0f, 1);
                return true;
            default:
                result = TriadSolverResult.Zero;
                return false;
        }
    }

    public static int PickBit(int mask, int step)
    {
        for (var bit = 0; (1 << bit) <= mask && bit < 31; bit++)
        {
            if ((mask & (1 << bit)) == 0)
            {
                continue;
            }

            step--;
            if (step < 0)
            {
                return bit;
            }
        }

        return -1;
    }
}

public sealed class TriadRandomAgent : TriadAgent
{
    private Random random;

    public TriadRandomAgent(int sessionSeed)
    {
        random = new Random(sessionSeed);
        SessionSeed = sessionSeed;
    }

    public override void Initialize(int sessionSeed)
    {
        SessionSeed = sessionSeed;
        random = new Random(sessionSeed);
    }

    // Starts at a random slot and walks forward to the first free cell and the first card in hand.
    public override bool FindNextMove(TriadSolver solver, TriadGameState state, out int cardIndex, out int boardPosition, out TriadSolverResult result)
    {
        cardIndex = -1;
        boardPosition = -1;
        result = TriadSolverResult.Zero;
        if (state.NumCardsPlaced < TriadGameState.BoardCells)
        {
            var testPosition = random.Next(TriadGameState.BoardCells);
            for (var pass = 0; pass < TriadGameState.BoardCells; pass++)
            {
                testPosition = (testPosition + 1) % TriadGameState.BoardCells;
                if (state.Board[testPosition].IsEmpty)
                {
                    boardPosition = testPosition;
                    break;
                }
            }
        }

        var mask = state.TurnDeck.AvailableCardMask;
        if (mask > 0)
        {
            var testIndex = random.Next(TriadDeckInstance.MaxAvailableCards);
            for (var pass = 0; pass < TriadDeckInstance.MaxAvailableCards; pass++)
            {
                testIndex = (testIndex + 1) % TriadDeckInstance.MaxAvailableCards;
                if ((mask & (1 << testIndex)) != 0)
                {
                    cardIndex = testIndex;
                    break;
                }
            }
        }

        return boardPosition >= 0 && cardIndex >= 0;
    }
}

// Exhaustive minimax over every card and cell: the owner picks its best branch, the opponent's branches are summed.
public abstract class TriadGraphAgent : TriadAgent
{
    // Sudden Death can restart the board; the cap keeps a Chaos match from recursing without end.
    private const int MaxSearchDepth = 20;

    private Random? failsafeRandom;

    public override bool FindNextMove(TriadSolver solver, TriadGameState state, out int cardIndex, out int boardPosition, out TriadSolverResult result)
    {
        cardIndex = -1;
        boardPosition = -1;
        if (!IsFinished(state, out result))
        {
            SearchActionSpace(solver, state, 0, out cardIndex, out boardPosition, out result);
        }

        return cardIndex >= 0 && boardPosition >= 0;
    }

    protected virtual TriadSolverResult SearchActionSpace(TriadSolver solver, TriadGameState state, int searchLevel, out int bestCardIndex, out int bestBoardPosition, out TriadSolverResult bestResult)
    {
        bestCardIndex = -1;
        bestBoardPosition = -1;
        bestResult = TriadSolverResult.Zero;
        if (searchLevel > MaxSearchDepth)
        {
            return bestResult;
        }

        var winsTotal = 0f;
        var drawsTotal = 0f;
        long gamesTotal = 0;
        solver.FindAvailableActions(state, out var boardMask, out var boardCount, out var cardsMask, out var cardsCount);
        if (cardsCount > 0 && boardCount > 0)
        {
            var owner = state.TurnOwner;
            var hasValidPlacement = false;
            for (var cardIndex = 0; cardIndex < TriadDeckInstance.MaxAvailableCards; cardIndex++)
            {
                if ((cardsMask & (1 << cardIndex)) == 0)
                {
                    continue;
                }

                for (var cell = 0; cell < TriadGameState.BoardCells; cell++)
                {
                    if ((boardMask & (1 << cell)) == 0)
                    {
                        continue;
                    }

                    var copy = new TriadGameState(state);
                    if (!solver.Simulation.PlaceCard(copy, cardIndex, copy.TurnDeck, owner, cell))
                    {
                        continue;
                    }

                    if (!IsFinished(copy, out var branchResult))
                    {
                        copy.ForcedCardIndex = -1;
                        branchResult = SearchActionSpace(solver, copy, searchLevel + 1, out _, out _, out _);
                    }

                    if (branchResult.IsBetterThan(bestResult) || !hasValidPlacement)
                    {
                        bestResult = branchResult;
                        bestCardIndex = cardIndex;
                        bestBoardPosition = cell;
                    }

                    winsTotal += branchResult.Wins;
                    drawsTotal += branchResult.Draws;
                    gamesTotal += branchResult.Games;
                    hasValidPlacement = true;
                }
            }

            if (!hasValidPlacement)
            {
                failsafeRandom ??= new Random(SessionSeed);
                bestCardIndex = PickBit(cardsMask, failsafeRandom.Next(cardsCount));
                bestBoardPosition = PickBit(boardMask, failsafeRandom.Next(boardCount));
            }
        }

        return searchLevel % 2 == 0 ? bestResult : new TriadSolverResult(winsTotal, drawsTotal, gamesTotal);
    }
}

// Below the search depth that stays exhaustive, a position is valued by random playouts instead.
public class TriadRolloutAgent : TriadGraphAgent
{
    private const int RolloutBatchSize = 64;

    public static int RolloutCount { get; set; } = 2000;

    public static int MaxParallelism { get; set; } = Math.Max(1, Environment.ProcessorCount);

    protected override TriadSolverResult SearchActionSpace(TriadSolver solver, TriadGameState state, int searchLevel, out int bestCardIndex, out int bestBoardPosition, out TriadSolverResult bestResult)
    {
        if (!CanRunRandomExploration(state, searchLevel))
        {
            return base.SearchActionSpace(solver, state, searchLevel, out bestCardIndex, out bestBoardPosition, out bestResult);
        }

        bestCardIndex = -1;
        bestBoardPosition = -1;
        bestResult = FindWinningProbability(solver, state);
        return bestResult;
    }

    protected virtual bool CanRunRandomExploration(TriadGameState state, int searchLevel) => searchLevel > 0;

    // Normalised to one game, so a rollout branch weighs the same as a single finished branch when the opponent level sums them.
    private TriadSolverResult FindWinningProbability(TriadSolver solver, TriadGameState state)
    {
        var rollouts = RolloutCount;
        var wins = 0;
        var draws = 0;
        var options = new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism };
        using var workerSolvers = new ThreadLocal<TriadSolver>(solver.CreateWorkerCopy);
        using var workerAgents = new ThreadLocal<TriadRandomAgent>(() => new TriadRandomAgent(SessionSeed + Environment.CurrentManagedThreadId));
        for (var completed = 0; completed < rollouts; completed += RolloutBatchSize)
        {
            var batchEnd = Math.Min(completed + RolloutBatchSize, rollouts);
            Parallel.For(completed, batchEnd, options, rolloutIndex =>
            {
                var copy = new TriadGameState(state);
                var agent = workerAgents.Value!;
                agent.Initialize(SessionSeed + rolloutIndex);
                workerSolvers.Value!.RunSimulation(copy, agent, agent);
                if (copy.Status == TriadGameStatus.BlueWins)
                {
                    Interlocked.Increment(ref wins);
                }
                else if (copy.Status == TriadGameStatus.BlueDraw)
                {
                    Interlocked.Increment(ref draws);
                }
            });
        }

        return new TriadSolverResult((float)wins / rollouts, (float)draws / rollouts, 1);
    }
}

// Searches exhaustively only once the remaining tree fits in MaxStatesToExplore, otherwise falls back to playouts.
public sealed class TriadLiveAgent : TriadRolloutAgent
{
    public const long MaxStatesToExplore = 10_000;

    private int minPlacedToExplore = 10;
    private int minPlacedToExploreWithForced = 10;

    public override void Initialize(int sessionSeed)
    {
        base.Initialize(sessionSeed);
        long forcedStates = 1;
        long states = 1;
        for (var toPlace = 1; toPlace <= TriadGameState.BoardCells; toPlace++)
        {
            var placed = TriadGameState.BoardCells - toPlace;
            forcedStates *= toPlace;
            if (forcedStates <= MaxStatesToExplore)
            {
                minPlacedToExploreWithForced = placed;
            }

            states *= toPlace * ((toPlace + 2) / 2) * ((toPlace + 1) / 2);
            if (states <= MaxStatesToExplore)
            {
                minPlacedToExplore = placed;
            }
        }
    }

    protected override bool CanRunRandomExploration(TriadGameState state, int searchLevel)
    {
        var threshold = state.ForcedCardIndex < 0 ? minPlacedToExplore : minPlacedToExploreWithForced;
        return searchLevel > 0 && state.NumCardsPlaced < threshold;
    }
}
