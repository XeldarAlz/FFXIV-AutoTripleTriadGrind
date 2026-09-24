using AutoTripleTriadGrind.Core.Localization;
using AutoTripleTriadGrind.Core.Planning;
using AutoTripleTriadGrind.Core.Triad.Data;

namespace AutoTripleTriadGrind.Windows;

internal static class TriadLauncher
{
    public enum Readiness : byte
    {
        DataUnavailable,
        NothingPicked,
        NothingReachable,
        AllDone,
        Ready,
    }

    public readonly record struct Assessment(TriadRunMode Mode, Readiness Readiness, int Cards, int Npcs, CollectPlanner.Plan Plan);

    // Planning checks every NPC's unlock state, so the answer is reused for this long.
    private const long RefreshMs = 500;

    private static long refreshedAtTick = -RefreshMs;
    private static int selectionVersion = -1;
    private static TriadRunMode cachedMode;
    private static Assessment cached;
    private static CachedText sublabelText;

    public static int SelectionVersion { get; private set; }

    public static void MarkSelectionChanged() => SelectionVersion++;

    public static void Start(TriadRunMode mode) => Plugin.Instance.Controller.Start(mode);

    public static Assessment Assess(Configuration configuration)
    {
        var mode = configuration.RunMode;
        var now = Environment.TickCount64;
        if (now - refreshedAtTick < RefreshMs && selectionVersion == SelectionVersion && cachedMode == mode)
        {
            return cached;
        }

        TriadData.EnsureLoaded();
        TriadOwnership.Refresh();
        cached = mode == TriadRunMode.Farm ? AssessFarm(configuration) : AssessCollect(configuration);
        refreshedAtTick = now;
        selectionVersion = SelectionVersion;
        cachedMode = mode;
        return cached;
    }

    public static string Sublabel(in Assessment assessment)
    {
        var key = ((long)assessment.Mode << 48) | ((long)assessment.Cards << 24) | (uint)assessment.Npcs;
        return sublabelText.Get(key, static packed =>
        {
            var npcs = (int)(packed & 0xFFFFFF);
            var cards = (int)((packed >> 24) & 0xFFFFFF);
            return (TriadRunMode)(packed >> 48) == TriadRunMode.Farm
                ? Loc.Plural(L.Triad.NpcsCount, npcs)
                : Loc.T(L.Triad.StartSub, Loc.Plural(L.Triad.CardsCount, cards), Loc.Plural(L.Triad.NpcsCount, npcs));
        });
    }

    public static string Reason(in Assessment assessment) => assessment.Readiness switch
    {
        Readiness.DataUnavailable  => Loc.T(L.Triad.ReasonData),
        Readiness.NothingPicked    => Loc.T(assessment.Mode == TriadRunMode.Farm ? L.Triad.ReasonPickNpc : L.Triad.ReasonPickCard),
        Readiness.NothingReachable => Loc.T(L.Triad.ReasonUnreachable),
        Readiness.AllDone          => Loc.T(L.Triad.ReasonAllOwned),
        _                          => string.Empty,
    };

    private static Assessment AssessCollect(Configuration configuration)
    {
        if (!TriadData.Loaded)
        {
            return new Assessment(TriadRunMode.Collect, Readiness.DataUnavailable, 0, 0, CollectPlanner.Plan.Empty);
        }

        if (configuration.SelectedCards.Count == 0)
        {
            return new Assessment(TriadRunMode.Collect, Readiness.NothingPicked, 0, 0, CollectPlanner.Plan.Empty);
        }

        var plan = CollectPlanner.Build(configuration);
        if (plan.WantedCards == 0)
        {
            return new Assessment(TriadRunMode.Collect, Readiness.AllDone, 0, 0, plan);
        }

        var reachableCards = plan.WantedCards - plan.Unavailable.Length;
        var readiness = plan.Assignments.Length == 0 ? Readiness.NothingReachable : Readiness.Ready;
        return new Assessment(TriadRunMode.Collect, readiness, reachableCards, plan.Assignments.Length, plan);
    }

    private static Assessment AssessFarm(Configuration configuration)
    {
        if (!TriadData.Loaded)
        {
            return new Assessment(TriadRunMode.Farm, Readiness.DataUnavailable, 0, 0, CollectPlanner.Plan.Empty);
        }

        var set = TriadData.Set;
        var eligible = 0;
        var farmNpcs = configuration.FarmNpcs;
        for (var index = 0; index < farmNpcs.Count; index++)
        {
            if (set.NpcIndexByTriadRowId.TryGetValue(farmNpcs[index], out var npcIndex) && NpcEligibility.Check(npcIndex, configuration) == SkipReason.None)
            {
                eligible++;
            }
        }

        var readiness = farmNpcs.Count == 0 ? Readiness.NothingPicked : eligible == 0 ? Readiness.NothingReachable : Readiness.Ready;
        return new Assessment(TriadRunMode.Farm, readiness, 0, eligible, CollectPlanner.Plan.Empty);
    }
}
