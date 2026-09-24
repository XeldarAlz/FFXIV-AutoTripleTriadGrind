namespace AutoTripleTriadGrind;

public sealed partial class Configuration
{
    public HashSet<ushort> SelectedCards { get; set; } = [];

    public List<uint> FarmNpcs { get; set; } = [];
    public FarmStopKind FarmStop { get; set; } = FarmStopKind.Matches;
    public int FarmMatchLimit { get; set; } = 50;

    public LibraryView LibraryView { get; set; } = LibraryView.ByNpc;
    public LibraryFilter LibraryFilter { get; set; } = LibraryFilter.Missing;
}
