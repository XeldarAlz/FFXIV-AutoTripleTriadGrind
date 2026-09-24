using AutoTripleTriadGrind.Core.Triad.Data;
using AutoTripleTriadGrind.Core.Triad.Logic;
using AutoTripleTriadGrind.Core.Triad.Logic.Rules;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTripleTriadGrind.Core.Triad.Decks;

public sealed class CachedDeck
{
    public ushort[] Cards { get; set; } = [];
    public float WinChance { get; set; }
    public int OwnedCount { get; set; }
    public ushort RuleMask { get; set; }
}

// Builds and caches one optimized deck per NPC. A cached deck is reused while the owned card count and the rules it
// was built for are unchanged, so a long farm does not rebuild before every match.
internal static class TriadDeckBuilder
{
    private static CancellationTokenSource? running;
    private static Task<TriadOptimizedDeck>? build;
    private static TriadDeckOptimizer? optimizer;
    private static uint buildingFor;
    private static ushort buildingRules;

    public static bool IsBuilding => build is { IsCompleted: false };

    public static float Progress => optimizer?.Progress ?? 0f;

    public static TriadDeck NpcDeck(int npcIndex)
    {
        var set = TriadData.Set;
        return new TriadDeck(set.FixedCardsOf(npcIndex), set.VariableCardsOf(npcIndex));
    }

    public static ushort RuleMask(int npcIndex, ReadOnlySpan<TriadRuleId> regional)
    {
        var mask = TriadData.Set.Npcs[npcIndex].RuleMask;
        for (var index = 0; index < regional.Length; index++)
        {
            if (regional[index] != TriadRuleId.None)
            {
                mask = TriadRuleIds.With(mask, regional[index]);
            }
        }

        return mask;
    }

    public static bool TryGetCached(Configuration configuration, int npcIndex, ushort ruleMask, out CachedDeck deck)
    {
        var rowId = TriadData.Set.Npcs[npcIndex].TriadRowId;
        if (configuration.OptimizedDecks.TryGetValue(rowId, out deck!) && deck.RuleMask == ruleMask
            && deck.OwnedCount == TriadOwnership.OwnedCount && deck.Cards.Length == TriadDeck.HandSize)
        {
            return true;
        }

        deck = null!;
        return false;
    }

    public static void Start(Configuration configuration, int npcIndex, ushort ruleMask)
    {
        var rowId = TriadData.Set.Npcs[npcIndex].TriadRowId;
        if (IsBuilding && buildingFor == rowId && buildingRules == ruleMask)
        {
            return;
        }

        Cancel();
        var owned = OwnedCards();
        optimizer = new TriadDeckOptimizer(NpcDeck(npcIndex), TriadRules.FromMask(ruleMask), owned);
        running = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(5, configuration.OptimizerTimeoutSeconds)));
        buildingFor = rowId;
        buildingRules = ruleMask;
        var threads = configuration.OptimizerThreads > 0 ? configuration.OptimizerThreads : Math.Max(1, Environment.ProcessorCount - 1);
        var job = optimizer;
        var token = running.Token;
        build = Task.Run(() => job.Run(threads, token), CancellationToken.None);
        RunLog.Info($"Building a deck for {TriadData.Set.NpcNames[npcIndex]} from {owned.Length} cards ({job.PossibleDecks} candidates).");
    }

    // Stores the finished deck in the cache and returns it; null while the build is still running.
    public static CachedDeck? TryCollect(Configuration configuration)
    {
        if (build is not { IsCompleted: true } finished)
        {
            return null;
        }

        build = null;
        if (finished.IsFaulted || finished.IsCanceled)
        {
            RunLog.Warning($"The deck build failed: {finished.Exception?.GetBaseException().Message}");
            return null;
        }

        var result = finished.Result;
        var cached = new CachedDeck
        {
            Cards = result.Cards,
            WinChance = result.WinChance,
            OwnedCount = TriadOwnership.OwnedCount,
            RuleMask = buildingRules,
        };
        configuration.OptimizedDecks[buildingFor] = cached;
        configuration.SaveDebounced();
        RunLog.Info($"Deck built: [{string.Join(", ", result.Cards)}], {result.WinChance:P0} estimated, {result.TestedDecks}/{result.PossibleDecks} decks tried.");
        return cached;
    }

    public static void Cancel()
    {
        running?.Cancel();
        running?.Dispose();
        running = null;
        build = null;
        optimizer = null;
    }

    private static ushort[] OwnedCards()
    {
        var cardIds = TriadData.Set.CardIdsInOrder;
        var owned = new List<ushort>(TriadOwnership.OwnedCount);
        for (var index = 0; index < cardIds.Length; index++)
        {
            if (TriadOwnership.IsOwned(cardIds[index]))
            {
                owned.Add(cardIds[index]);
            }
        }

        return owned.ToArray();
    }
}
