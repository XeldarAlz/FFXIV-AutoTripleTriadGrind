using AutoTripleTriadGrind.Core.Triad.Data;
using AutoTripleTriadGrind.Core.Triad.Logic.Agents;
using AutoTripleTriadGrind.Core.Triad.Logic.Rules;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTripleTriadGrind.Core.Triad.Logic;

public readonly record struct TriadOptimizedDeck(ushort[] Cards, float WinChance, long TestedDecks, long PossibleDecks);

// Builds candidate decks from the best-scored owned cards, then plays each against the NPC with random agents and
// keeps the one that wins most. The best cards come first, so a search cut short by its time limit still returns a
// strong deck.
public sealed partial class TriadDeckOptimizer
{
    private const int GamesPerDeck = 2000;
    private const int PriorityToBuild = 10;
    private const int CommonToBuild = 20;
    private const int CommonPercentDroppedPerPrioritySlot = 10;
    // Cards up to three stars fill any slot; four and five star cards are capped by the game's deck rules.
    private const int CommonMaxStars = 3;
    private const int FourStarSlots = 2;
    private const int FiveStarSlots = 1;
    private const int DeckSlotCommon = -1;

    private static readonly ushort[] starterDeck = [1, 3, 6, 7, 10];

    private readonly TriadSolver solver = new(new TriadRandomAgent(0));
    private readonly TriadDeck npcDeck;
    private readonly bool deckOrderMatters;
    private CardPool pool;
    private long possibleDecks;
    private long testedDecks;

    public TriadDeckOptimizer(TriadDeck npcDeck, IReadOnlyList<TriadRule> rules, ReadOnlySpan<ushort> ownedCards)
    {
        this.npcDeck = npcDeck;
        solver.InitializeSimulation(rules);
        var simulationRules = solver.Simulation.Rules;
        for (var index = 0; index < simulationRules.Count; index++)
        {
            deckOrderMatters |= simulationRules[index].DeckOrderMatters;
        }

        HasPool = BuildCardPool(ownedCards, simulationRules);
        if (HasPool)
        {
            possibleDecks = CountPossibleDecks();
        }
    }

    public bool HasPool { get; }

    public long PossibleDecks => possibleDecks;

    public float Progress => possibleDecks > 0 ? Math.Clamp(Interlocked.Read(ref testedDecks) / (float)possibleDecks, 0f, 1f) : 0f;

    public static float CardScore(in TriadCard card)
    {
        const float averageWeight = 1f;
        const float maximumWeight = 0.75f;
        const float rarityWeight = 0.2f;
        const float maximumScore = averageWeight + maximumWeight + rarityWeight;
        var highest = Math.Max(Math.Max(card.Up, card.Right), Math.Max(card.Down, card.Left));
        var average = card.SideSum / 4f;
        var score = average / 10f * averageWeight + highest / 10f * maximumWeight + (card.Stars - 1) / 4f * rarityWeight;
        return score / maximumScore;
    }

    public TriadOptimizedDeck Run(int threads, CancellationToken cancelToken)
    {
        if (!HasPool)
        {
            return new TriadOptimizedDeck(starterDeck, 0f, 0, 0);
        }

        var sync = new object();
        var bestScore = 0;
        var bestDeck = starterDeck;
        using var workers = new ThreadLocal<TriadSolver>(solver.CreateWorkerCopy);
        var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, threads), CancellationToken = cancelToken };
        try
        {
            Parallel.ForEach(new SlotIterator(pool).Decks(), options, candidate =>
            {
                if (!candidate.IsValid)
                {
                    return;
                }

                var testDeck = new TriadDeck(candidate.Cards, []);
                var score = ScoreDeck(workers.Value!, testDeck, candidate.Seed, cancelToken);
                Interlocked.Increment(ref testedDecks);
                if (score <= Volatile.Read(ref bestScore))
                {
                    return;
                }

                lock (sync)
                {
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestDeck = candidate.Cards;
                    }
                }
            });
        }
        catch (OperationCanceledException)
        {
        }

        return new TriadOptimizedDeck(bestDeck, bestScore / (GamesPerDeck * 2f), Interlocked.Read(ref testedDecks), possibleDecks);
    }

    // A win scores 2 and a draw 1, over the same number of games with each side opening.
    private int ScoreDeck(TriadSolver worker, TriadDeck testDeck, int seed, CancellationToken cancelToken)
    {
        var agent = new TriadRandomAgent(seed);
        var score = 0;
        for (var game = 0; game < GamesPerDeck / 2; game++)
        {
            if (cancelToken.IsCancellationRequested)
            {
                break;
            }

            var redOpens = worker.StartSimulation(testDeck, npcDeck, TriadGameStatus.InProgressRed);
            worker.RunSimulation(redOpens, agent, agent);
            score += Points(redOpens.Status);

            var blueOpens = worker.StartSimulation(testDeck, npcDeck, TriadGameStatus.InProgressBlue);
            worker.RunSimulation(blueOpens, agent, agent);
            score += Points(blueOpens.Status);
        }

        return score;
    }

    private static int Points(TriadGameStatus status) => status switch
    {
        TriadGameStatus.BlueWins => 2,
        TriadGameStatus.BlueDraw => 1,
        _                        => 0,
    };

    private long CountPossibleDecks()
    {
        long count = 1;
        var commonSlots = 0;
        for (var slot = 0; slot < pool.SlotTypes.Length; slot++)
        {
            var slotType = pool.SlotTypes[slot];
            if (slotType == DeckSlotCommon)
            {
                commonSlots++;
            }
            else
            {
                count *= pool.PriorityLists[slotType].Length;
            }
        }

        long factorial = 1;
        for (var index = 0; index < commonSlots; index++)
        {
            count *= pool.CommonList.Length - index;
            factorial *= index + 1;
        }

        return count / factorial;
    }
}
