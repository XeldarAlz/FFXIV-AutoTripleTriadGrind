using AutoTripleTriadGrind.Core.Triad.Data;

namespace AutoTripleTriadGrind;

public sealed partial class Configuration
{
    public HashSet<ushort> SelectedCards { get; set; } = [];

    public List<uint> FarmNpcs { get; set; } = [];
    public FarmStopKind FarmStop { get; set; } = FarmStopKind.Matches;
    public int FarmMatchLimit { get; set; } = 50;

    // Keyed by TripleTriad row id: the regional rules last seen on an NPC's challenge window, and the deck built for it.
    public Dictionary<uint, TriadRuleId[]> RegionalRules { get; set; } = [];
    public Dictionary<uint, Core.Triad.Decks.CachedDeck> OptimizedDecks { get; set; } = [];

    public LibraryView LibraryView { get; set; } = LibraryView.ByNpc;
    public LibraryFilter LibraryFilter { get; set; } = LibraryFilter.Missing;
}
