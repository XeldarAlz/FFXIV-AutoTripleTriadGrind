namespace AutoTripleTriadGrind.Core.Stats;

[Serializable]
public sealed class RunRecord
{
    public TriadRunMode Mode { get; set; } = TriadRunMode.Collect;

    public DateTime StartedAtUtc { get; set; }
    public DateTime EndedAtUtc { get; set; }
    public double DurationSeconds { get; set; }

    public int MatchesWon { get; set; }
    public int MatchesLost { get; set; }
    public int MatchesDrawn { get; set; }
    public int NpcsCompleted { get; set; }
    public int NpcsSkipped { get; set; }
    public List<ushort> CardsObtained { get; set; } = [];

    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds);
    public int MatchesPlayed => MatchesWon + MatchesLost + MatchesDrawn;
    public double WinRate => MatchesPlayed > 0 ? (double)MatchesWon / MatchesPlayed : 0;
}
